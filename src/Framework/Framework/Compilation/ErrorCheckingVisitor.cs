using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Parser;
using DotVVM.Framework.Compilation.Parser.Binding.Tokenizer;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;
using DotVVM.Framework.Compilation.Parser.Dothtml.Tokenizer;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Compilation
{
    public class ErrorCheckingVisitor : ResolvedControlTreeVisitor
    {
        public List<DotvvmCompilationDiagnostic> Diagnostics { get; } = new();
        public string? FileName { get; set; }

        public ErrorCheckingVisitor(string? fileName)
        {
            this.FileName = fileName;
        }

        private void AddNodeErrors(DothtmlNode node, int priority)
        {
            if (!node.HasNodeErrors && !node.HasNodeWarnings)
                return;

            var location = new DotvvmCompilationSourceLocation(node) { FileName = FileName };
            foreach (var error in node.NodeErrors)
            {
                Diagnostics.Add(new DotvvmCompilationDiagnostic(error, DiagnosticSeverity.Error, location) { Priority = priority });
            }
            foreach (var warning in node.NodeWarnings)
            {
                Diagnostics.Add(new DotvvmCompilationDiagnostic(warning, DiagnosticSeverity.Warning, location));
            }
        }

        DotvvmCompilationSourceLocation? MapBindingLocation(ResolvedBinding binding, DotvvmProperty? relatedProperty, BindingCompilationException error)
        {
            var tokens = error.Tokens?.ToArray();
            if (tokens is null or {Length:0})
                return null;

            var valueNode = (binding.BindingNode as DothtmlBindingNode)?.ValueNode;
            var valueToken = valueNode?.ValueToken;

            if (valueToken is null)
            {
                // create anonymous file for the one binding
                var file = new MarkupFile("anonymous binding", "anonymos binding", error.Expression ?? "");
                return new DotvvmCompilationSourceLocation(file.FileName, file, tokens) { RelatedSyntaxNode = valueNode ?? binding.DothtmlNode, RelatedResolvedNode = binding, RelatedBinding = binding.Binding, RelatedProperty = relatedProperty };
            }
            else
            {
                tokens = tokens.Select(t => t switch {
                    BindingToken bt => bt.RemapPosition(valueToken),
                    _ => t // dothtml tokens most likely already have correct position
                }).ToArray();
                return new DotvvmCompilationSourceLocation(binding, valueNode, tokens) { RelatedBinding = binding.Binding, RelatedProperty = relatedProperty };
            }
        }

        /// <summary>
        /// Assigns locations to the provied exceptions:
        /// * if a BindingCompilationException with location, it and all its (nested) InnerException are assigned this location
        /// * the locations are processed using MapBindingLocation to make them useful in the context of a dothtml file </summary>
        Dictionary<Exception, DotvvmCompilationSourceLocation?> AnnotateBindingExceptionWithLocation(ResolvedBinding binding, DotvvmProperty? relatedProperty, IEnumerable<Exception> errors)
        {
            var result = new Dictionary<Exception, DotvvmCompilationSourceLocation?>(ReferenceEqualityComparer.Instance);
            void recurse(Exception exception, DotvvmCompilationSourceLocation? location)
            {
                if (result.ContainsKey(exception))
                    return;

                if (exception is BindingCompilationException bce)
                    location = MapBindingLocation(binding, relatedProperty, bce) ?? location;

                if (location is {})
                    result[exception] = location;

                if (exception is AggregateException agg)
                    foreach (var inner in agg.InnerExceptions)
                        recurse(inner, location);
                else if (exception.InnerException is {})
                    recurse(exception.InnerException, location);
            }

            foreach (var x in errors)
                recurse(x, null);

            return result;
        }

        public override void VisitPropertyBinding(ResolvedPropertyBinding propertyBinding)
        {
            var errors = propertyBinding.Binding.Errors;
            if (errors.HasErrors)
            {
                var bindingLocation = new DotvvmCompilationSourceLocation(propertyBinding.Binding, propertyBinding.Binding.BindingNode);
                var detailedLocations = AnnotateBindingExceptionWithLocation(propertyBinding.Binding, propertyBinding.Property, errors.Exceptions);
                foreach (var error in
                    from topException in errors.Exceptions
                    from exception in topException.AllInnerExceptions()
                    where exception is not AggregateException and not BindingPropertyException { InnerException: {} } and not BindingCompilationException { InnerException: {}, Message: "Binding compilation failed" }
                    let location = detailedLocations.GetValueOrDefault(exception)
                    let message = exception.Message
                    orderby location?.LineNumber ?? int.MaxValue,
                            location?.ColumnNumber ?? int.MaxValue,
                            location?.LineErrorLength ?? int.MaxValue,
                            exception.InnerException is null ? 0 : exception is BindingCompilationException ? -1 : 2
                    group (topException, exception, location, message) by message into g
                    select g.First())
                {
                    var message = $"{error.exception.GetType().Name}: {error.message}";
                    Diagnostics.Add(new DotvvmCompilationDiagnostic(
                        message,
                        DiagnosticSeverity.Error,
                        error.location ?? bindingLocation,
                        innerException: error.topException
                    ));
                }
                // summary error explaining which binding properties are causing the problem
                Diagnostics.Add(new DotvvmCompilationDiagnostic(
                    errors.GetErrorMessage(propertyBinding.Binding.Binding),
                    DiagnosticSeverity.Error,
                    bindingLocation,
                    innerException: errors.Exceptions.FirstOrDefault()
                ));
            }
            base.VisitPropertyBinding(propertyBinding);
        }

        public override void VisitView(ResolvedTreeRoot view)
        {
            base.VisitView(view);
        }

        public void AddTokenizerErrors(List<DothtmlToken> tokens)
        {
            foreach (var token in tokens)
            {
                if (token.Error is { IsCritical: var critical })
                {
                    var location = new DotvvmCompilationSourceLocation(new[] { (token.Error as BeginWithLastTokenOfTypeTokenError<DothtmlToken, DothtmlTokenType>)?.LastToken ?? token });
                    Diagnostics.Add(new DotvvmCompilationDiagnostic(
                        token.Error.ErrorMessage,
                        critical ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                        location
                    ) { Priority = 200 });
                }
            }
        }
        public void AddSyntaxErrors(DothtmlNode rootNode)
        {
            foreach (var node in rootNode.EnumerateNodes())
            {
                AddNodeErrors(node, priority: 100);
            }
        }

        public void ThrowOnErrors()
        {
            var sorted = Diagnostics.OrderBy(e => (-e.Priority, e.Location.LineNumber ?? -1, e.Location.ColumnNumber ?? -1)).ToArray();
            if (sorted.FirstOrDefault(e => e.IsError) is {} error)
            {
                throw new DotvvmCompilationException(error, sorted);
            }
        }
    }
}
