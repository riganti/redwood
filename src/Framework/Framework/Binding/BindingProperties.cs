using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Immutable;
using DotVVM.Framework.Runtime.Filters;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using System.Reflection;

namespace DotVVM.Framework.Binding.Properties
{
    /// <summary>
    /// Contains Javascript evaluable in knockout binding, knockout context parameters are represented as symbolic parameters in the ParametrizedCode
    /// </summary>
    /// <param name="Code">Knockout binding expression. May return observable.</param>
    /// <param name="UnwrappedCode">Knockout binding expression. Always unwraps the observable.</param>
    /// <param name="WrappedCode">Knockout binding expression. Always returns an observable.</param>
    public sealed record KnockoutExpressionBindingProperty (
        ParametrizedCode Code,
        ParametrizedCode UnwrappedCode,
        ParametrizedCode WrappedCode
    );

    /// <summary>
    /// Contains string that identifies the translated binding.
    /// </summary>
    public sealed record SimplePathExpressionBindingProperty(
        ParametrizedCode Code
    );

    /// <summary>
    /// Contains original binding string, as it was typed in dothtml file. (it is trimmed)
    /// </summary>
    public sealed record OriginalStringBindingProperty(
        string Code
    );

    /// <summary>
    /// Contains binding's result type.
    /// </summary>
    public sealed record ResultTypeBindingProperty(
        Type Type
    );

    /// <summary>
    /// Contains unique id of binding in its DataContext and the page
    /// </summary>
    public sealed record IdBindingProperty(
        string Id
    );

    /// <summary>
    /// Contains JS code, that will invoke the command. May contain symbolic parameters from `JavascriptTranslator` and `CommandBindingExpression`
    /// </summary>
    public sealed record CommandJavascriptBindingProperty(
        ParametrizedCode Code
    );

    /// <summary>
    /// Contains JS code, that will invoke the static command wrapped in (options) => ... lambda. May contain symbolic parameters from `CommandBindingExpression`, knockout context is taken from the options
    /// </summary>
    public sealed record StaticCommandOptionsLambdaJavascriptProperty(
        ParametrizedCode Code
    );
    /// <summary>
    /// Contains JS code, that will invoke the static command. May contain symbolic parameters from `JavascriptTranslator` and `CommandBindingExpression`
    /// </summary>
    [Obsolete("Deprecated in favor of StaticCommandOptionsLambdaJavascriptProperty. It should contain the same code as this property, but wrapped in a lambda function taking PostbackOption. It will use options.knockoutContext and options.viewModel instead of ko.contextFor(this)")]
    public sealed record StaticCommandJavascriptProperty(
        ParametrizedCode Code
    );

    /// <summary>
    /// Contains JS code, that will invoke the static command. May contain symbolic parameters from `JavascriptTranslator` and `CommandBindingExpression`
    /// </summary>
    public sealed record StaticCommandJsAstProperty(
        JsExpression Expression
    );

    /// <summary>
    /// Contains <see cref="System.Linq.Expressions.Expression"/> instance that represents code as it was written in markup with minimal processing.
    /// </summary>
    public sealed record ParsedExpressionBindingProperty(
        Expression Expression
    );

    /// <summary>
    /// Contains <see cref="System.Linq.Expressions.Expression"/> instance that represents code converted to be evaluated as binding (type conversions applied, ...). 
    /// </summary>
    public sealed record CastedExpressionBindingProperty(
        Expression Expression
    );

    /// <summary>
    /// Contains raw translated JS AST that came from JavascriptTranslator. Specifically it has type annotations on it and does not include observable unwraps and null-checks.
    /// </summary>
    public sealed record KnockoutJsExpressionBindingProperty(
        JsExpression Expression
    );

    /// <summary>
    /// Contains action filters that should be invoked before the binding invocation.
    /// </summary>
    public sealed record ActionFiltersBindingProperty(
        ImmutableArray<IActionFilter> Filters
    );

    /// <summary>
    /// Contains expected type of the binding - typically type of the bound property.
    /// </summary>
    public sealed record ExpectedTypeBindingProperty(
        Type Type
    );
 
    /// <summary>
    /// Contains the property where the binding is assigned.
    /// </summary>
    public sealed record AssignedPropertyBindingProperty(
        DotvvmProperty DotvvmProperty
    );

    /// <summary>
    /// Describes how severe a diagnostic is.
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>
        /// Something that is an issue, as determined by some authority,
        /// but is not surfaced through normal means.
        /// There may be different mechanisms that act on these issues.
        /// </summary>
        Hidden = 0,
 
        /// <summary>
        /// Information that does not indicate a problem (i.e. not prescriptive).
        /// </summary>
        Info = 1,
 
        /// <summary>
        /// Something suspicious but allowed.
        /// </summary>
        Warning = 2,
 
        /// <summary>
        /// Something not allowed by the rules of the language or other authority.
        /// </summary>
        Error = 3,
    }

    /// <summary>
    /// Contains (mutable) list of error that are produced during the binding lifetime.
    /// </summary>
    public sealed class BindingErrorReporterProperty
    {
        public ConcurrentStack<(Type req, Exception error, DiagnosticSeverity severity)> Errors = new ConcurrentStack<(Type req, Exception error, DiagnosticSeverity)>();
        public bool HasErrors => Errors.Any(e => e.severity == DiagnosticSeverity.Error);
        public string GetErrorMessage(IBinding binding)
        {
            var badRequirements = Errors.Where(e => e.severity == DiagnosticSeverity.Error).Select(e => e.req).Distinct().ToArray();
            return $"Could not initialize binding '{binding}', requirement{(badRequirements.Length > 1 ? "s" : "")} {string.Join<Type>(", ", badRequirements)} {(badRequirements.Length > 1 ? "were" : "was")} not met.";
        }
        public IEnumerable<Exception> Exceptions => Errors.Select(e => e.error);

        public override string ToString()
        {
            var errCount = Errors.Count(e => e.severity == DiagnosticSeverity.Error);
            var warnCount = Errors.Count(e => e.severity == DiagnosticSeverity.Warning);
            var infoCount = Errors.Count(e => e.severity == DiagnosticSeverity.Info);
            if (errCount + warnCount + infoCount == 0)
                return "No errors";

            var msgCount = string.Join(", ", new string?[] {
                errCount > 0 ? $"{errCount} errors" : null,
                warnCount > 0 ? $"{warnCount} warnings" : null,
                infoCount > 0 ? $"{infoCount} warnings" : null,
                }.OfType<string>());
            
            return $"{msgCount}: {string.Join("; ", Errors.Select(e => e.error?.Message))}";
        }
    }

    /// <summary>
    /// Contains a binding that unwraps <see cref="DotVVM.Framework.Controls.IBaseGridViewDataSet.Items"/>
    /// </summary>
    public sealed record DataSourceAccessBinding(IBinding Binding);

    /// <summary>
    /// Contains a binding that accesses $index-th element in the collection. Uses the <see cref="DotVVM.Framework.Compilation.ControlTree.CurrentCollectionIndexExtensionParameter"/>.
    /// </summary>
    public sealed record DataSourceCurrentElementBinding(IBinding Binding);

    /// <summary>
    /// Contains a binding that gets the collection's Length or Count
    /// </summary>
    public sealed record DataSourceLengthBinding(IBinding Binding);

    /// <summary> Contains a lambda function that gets the collection element for a given index.</summary>
    public sealed record SelectorItemBindingProperty(IValueBinding Expression);

    /// <summary> Which resources are requested by this binding.</summary>
    public sealed record RequiredRuntimeResourcesBindingProperty(
        ImmutableArray<string> Resources
    ) {
        public static readonly RequiredRuntimeResourcesBindingProperty Empty = new RequiredRuntimeResourcesBindingProperty(ImmutableArray<string>.Empty);
    }

    /// <summary> Specifies that globalize resource with the current culture is necessary for this binding. </summary>
    public sealed record GlobalizeResourceBindingProperty();

    /// <summary> Contains binding {value: _this} as the current data context. </summary>
    public sealed record ThisBindingProperty(IBinding binding);

    /// <summary> Contains <see cref="DotVVM.Framework.Compilation.ControlTree.DataContextStack">data context</see> which would be expected in a Repeater bound to this binding. </summary>
    public sealed record CollectionElementDataContextBindingProperty(
        DataContextStack DataContext
    );

    /// <summary> Contains a binding with the expression {thisBinding} > 0 </summary>
    public sealed record IsMoreThanZeroBindingProperty(IBinding Binding);
    /// <summary> Contains a binding with the expression !{thisBinding} </summary>
    public sealed record NegatedBindingExpression(IBinding Binding);
    /// <summary> Contains a binding with the expression {thisBinding} is null </summary>
    public sealed record IsNullBindingExpression(IBinding Binding);
    /// <summary> Contains a binding with the expression string.IsNullOrWhiteSpace({thisBinding}) </summary>
    public sealed record IsNullOrWhitespaceBindingExpression(IBinding Binding);
    /// <summary> Contains a binding with the expression string.IsNullOrEmpty({thisBinding}) </summary>
    public sealed record IsNullOrEmptyBindingExpression(IBinding Binding);
    /// <summary> Contains the same binding as this binding but converted to a string. </summary>
    public sealed record ExpectedAsStringBindingExpression(IBinding Binding);
    /// <summary> Contains references to the .NET properties referenced in the binding. MainProperty is the property on the root node (modulo conversions and simple expressions). </summary>
    public sealed record ReferencedViewModelPropertiesBindingProperty(PropertyInfo? MainProperty, PropertyInfo[] OtherProperties, IValueBinding? UnwrappedBindingExpression);
}
