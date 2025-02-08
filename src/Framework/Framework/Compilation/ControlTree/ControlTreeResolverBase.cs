using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Binding;
using System.Diagnostics.CodeAnalysis;
using DotVVM.Framework.Compilation.Directives;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation.ViewCompiler;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Binding.Expressions;
using FastExpressionCompiler;

namespace DotVVM.Framework.Compilation.ControlTree
{
    /// <summary>
    /// An abstract base class for control tree resolver.
    /// </summary>
    public abstract class ControlTreeResolverBase : IControlTreeResolver
    {
        protected readonly IControlResolver controlResolver;
        protected readonly IAbstractTreeBuilder treeBuilder;
        private readonly IMarkupDirectiveCompilerPipeline markupDirectiveCompilerPipeline;
        protected readonly DotvvmConfiguration configuration;
        protected Lazy<IControlResolverMetadata> rawLiteralMetadata;
        protected Lazy<IControlResolverMetadata> literalMetadata;
        protected Lazy<IControlResolverMetadata> placeholderMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlTreeResolverBase"/> class.
        /// </summary>
        public ControlTreeResolverBase(IControlResolver controlResolver, IAbstractTreeBuilder treeBuilder, IMarkupDirectiveCompilerPipeline markupDirectiveCompilerPipeline, DotvvmConfiguration configuration)
        {
            this.controlResolver = controlResolver;
            this.treeBuilder = treeBuilder;
            this.markupDirectiveCompilerPipeline = markupDirectiveCompilerPipeline;
            this.configuration = configuration;
            rawLiteralMetadata = new Lazy<IControlResolverMetadata>(() => controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(RawLiteral))));
            literalMetadata = new Lazy<IControlResolverMetadata>(() => controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(Literal))));
            placeholderMetadata = new Lazy<IControlResolverMetadata>(() => controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(PlaceHolder))));
        }

        /// <summary>
        /// Resolves the control tree.
        /// </summary>
        public virtual IAbstractTreeRoot ResolveTree(DothtmlRootNode root, string fileName)
        {
            var directiveMetadata = markupDirectiveCompilerPipeline.Compile(root, fileName);

            // We need to call BuildControlMetadata instead of ResolveControl. The control builder for the control doesn't have to be compiled yet so the
            // metadata would be incomplete and ResolveControl caches them internally. BuildControlMetadata just builds the metadata and the control is
            // actually resolved when the control builder is ready and the metadata are complete.
            var viewMetadata = controlResolver.BuildControlMetadata(CreateControlType(directiveMetadata.BaseType, fileName));

            var dataContextTypeStack = CreateDataContextTypeStack(directiveMetadata.ViewModelType, null, directiveMetadata.Imports, new BindingExtensionParameter[] {
                new CurrentMarkupControlExtensionParameter(directiveMetadata.BaseType),
                new BindingPageInfoExtensionParameter(),
                new BindingApiExtensionParameter()
            }.Concat(directiveMetadata.InjectedServices)
             .Concat(directiveMetadata.ViewModuleResult is null ? new BindingExtensionParameter[0] : new[] { directiveMetadata.ViewModuleResult.ExtensionParameter }).ToArray());

            // Resolve master page
            IAbstractControlBuilderDescriptor? masterPage = null;
            if (directiveMetadata.MasterPageDirective is IAbstractDirective masterPageDirective)
            {
                masterPage = ResolveMasterPage(fileName, masterPageDirective);
                ValidateMasterPage(directiveMetadata.ViewModelType, masterPageDirective, masterPage);
            }

            var view = treeBuilder.BuildTreeRoot(this, viewMetadata, root, dataContextTypeStack, directiveMetadata.Directives, masterPage);
            view.FileName = fileName;

            if (directiveMetadata.ViewModuleResult is { })
            {
                // Resolve viewmodule IDs
                var viewModuleId = AssignViewModuleId(masterPage);
                var viewModuleCompilationResult = directiveMetadata.ViewModuleResult;
                viewModuleCompilationResult.ExtensionParameter.Id = viewModuleId;
                viewModuleCompilationResult.Reference.ViewId = viewModuleId;

                treeBuilder.AddProperty(
                    view,
                    treeBuilder.BuildPropertyValue(Internal.ReferencedViewModuleInfoProperty, viewModuleCompilationResult.Reference, null),
                    out _
                );
            }

            ResolveRootContent(root, view, viewMetadata);

            return view;
        }

        protected abstract IAbstractControlBuilderDescriptor? ResolveMasterPage(string currentFile, IAbstractDirective masterPageDirective);

        protected virtual void ValidateMasterPage(ITypeDescriptor? viewModel, IAbstractDirective masterPageDirective, IAbstractControlBuilderDescriptor? masterPage)
        {
            if (masterPage is null || viewModel is null)
            {
                return;
            }

            if (masterPage.DataContextType is ResolvedTypeDescriptor typeDescriptor && typeDescriptor.Type == typeof(UnknownTypeSentinel))
            {
                masterPageDirective!.DothtmlNode!.AddError("Could not resolve the type of viewmodel for the specified master page. " +
                    $"This usually means that there is an error with the @viewModel directive in the master page file: \"{masterPage.FileName}\". " +
                    $"Make sure that the provided viewModel type is correct and visible for DotVVM.");
            }
            else if (!masterPage.DataContextType.IsAssignableFrom(viewModel))
            {
                masterPageDirective!.DothtmlNode!.AddError($"The viewmodel {viewModel.Name} is not assignable to the viewmodel of the master page {masterPage.DataContextType.Name}.");
            }
        }

        protected virtual string AssignViewModuleId(IAbstractControlBuilderDescriptor? masterPage)
        {
            var numberOfMasterPages = 0;
            while (masterPage != null)
            {
                masterPage = masterPage.MasterPage;
                numberOfMasterPages += 1;
            }
            return "p" + numberOfMasterPages;
        }

        /// <summary>
        /// Resolves the content of the root node.
        /// </summary>
        protected virtual void ResolveRootContent(DothtmlRootNode root, IAbstractControl view, IControlResolverMetadata viewMetadata)
        {
            // WORKAROUND:
            // if there is a control in root of a MarkupControl that has DataContext assigned, it will not find the data context space, because the space of DataContext property does not include the control itself and the space of MarkupControl also does not include the MarkupControl. And because the MarkupControl is a direct parent of the DataContext-bound control there is no space in between.

            if (viewMetadata.Type.IsAssignableTo(new ResolvedTypeDescriptor(typeof(DotvvmMarkupControl))))
            {
                var placeHolder = this.treeBuilder.BuildControl(
                    this.controlResolver.ResolveControl(new ResolvedTypeDescriptor(typeof(PlaceHolder))),
                    view.DothtmlNode,
                    view.DataContextTypeStack
                );
                this.treeBuilder.AddChildControl(view, placeHolder);
                view = placeHolder;
                viewMetadata = placeHolder.Metadata;
            }

            ResolveControlContentImmediately(view, root.Content);
        }

        /// <summary>
        /// Processes the parser node and builds a control.
        /// </summary>
        public IAbstractControl? ProcessNode(IAbstractTreeNode parent, DothtmlNode node, IControlResolverMetadata parentMetadata, IDataContextStack dataContext)
        {
            try
            {
                if (node is DothtmlBindingNode)
                {
                    // binding in text
                    return ProcessBindingInText(node, dataContext);
                }
                else if (node is DotHtmlCommentNode commentNode)
                {
                    // HTML comment
                    return ProcessHtmlComment(node, dataContext, commentNode);
                }
                else if (node is DothtmlLiteralNode literalNode)
                {
                    // text content
                    return ProcessText(node, parentMetadata, dataContext, literalNode);
                }
                else if (node is DothtmlElementNode element)
                {
                    // HTML element
                    return ProcessObjectElement(element, dataContext);
                }
                else
                {
                    throw new NotSupportedException($"The node of type '{node.GetType()}' is not supported!");
                }
            }
            catch (DotvvmCompilationException ex)
            {
                if (ex.Tokens.IsEmpty)
                {
                    var oldLoc = ex.CompilationError.Location;
                    ex.CompilationError = ex.CompilationError with {
                        Location = new(oldLoc.FileName, oldLoc.MarkupFile, node.Tokens)
                    };
                }
                if (!LogError(ex, node))
                    throw;
                else return null;
            }
            catch (Exception ex)
            {
                if (!LogError(ex, node))
                    throw new DotvvmCompilationException("", ex, node.Tokens);
                else return null;
            }
        }

        protected virtual bool LogError(Exception exception, DothtmlNode node)
        {
            return false;
        }

        private IAbstractControl ProcessText(DothtmlNode node, IControlResolverMetadata parentMetadata, IDataContextStack dataContext, DothtmlLiteralNode literalNode)
        {
            var whitespace = string.IsNullOrWhiteSpace(literalNode.Value);

            string text;
            if (literalNode.Escape)
            {
                text = WebUtility.HtmlEncode(literalNode.Value);
            }
            else
            {
                text = literalNode.Value;
            }

            var literal = treeBuilder.BuildControl(rawLiteralMetadata.Value, node, dataContext);
            literal.ConstructorParameters = new object[] { text.Replace("\r\n", "\n"), literalNode.Value.Replace("\r\n", "\n"), BoxingUtils.Box(whitespace) };
            return literal;
        }

        private IAbstractControl ProcessHtmlComment(DothtmlNode node, IDataContextStack dataContext, DotHtmlCommentNode commentNode)
        {
            var text = commentNode.IsServerSide ? "" : "<!--" + commentNode.Value + "-->";

            var literal = treeBuilder.BuildControl(rawLiteralMetadata.Value, node, dataContext);
            literal.ConstructorParameters = new object[] { text.Replace("\r\n", "\n"), "", BoxingUtils.True };
            return literal;
        }

        private IAbstractControl ProcessBindingInText(DothtmlNode node, IDataContextStack dataContext)
        {
            var bindingNode = (DothtmlBindingNode)node;
            var literal = treeBuilder.BuildControl(literalMetadata.Value, node, dataContext);

            var textBinding = ProcessBinding(bindingNode, dataContext, Literal.TextProperty);
            var textProperty = treeBuilder.BuildPropertyBinding(Literal.TextProperty, textBinding, null);
            treeBuilder.AddProperty(literal, textProperty, out _); // this can't fail

            var renderSpanElement = treeBuilder.BuildPropertyValue(Literal.RenderSpanElementProperty, BoxingUtils.False, null);
            treeBuilder.AddProperty(literal, renderSpanElement, out _);

            return literal;
        }

        /// <summary>
        /// Processes the HTML element that represents a new object.
        /// </summary>
        private IAbstractControl ProcessObjectElement(DothtmlElementNode element, IDataContextStack dataContext)
        {
            // build control
            var controlMetadata = controlResolver.ResolveControl(element.TagPrefix, element.TagName, out var constructorParameters);
            if (controlMetadata == null)
            {
                controlMetadata = controlResolver.ResolveControl("", element.TagName, out constructorParameters).NotNull();
                constructorParameters = new[] { element.FullTagName };
                var similarControls = FindSimilarControls(element.TagPrefix, element.TagName, controlBaseType: controlMetadata.Type);
                var similarNameHelp = similarControls.Any() ? $" Did you mean {string.Join(", ", similarControls.Select(c => c.tagPrefix + ":" + c.name))}, or other DotVVM control?" : "";
                var tagPrefixHelp = configuration.Markup.Controls.Any(c => string.Equals(c.TagPrefix, element.TagPrefix, StringComparison.OrdinalIgnoreCase))
                                        ? ""
                                        : $" {(similarNameHelp is "" ? "Make" : "Otherwise, make")} sure that the tagPrefix '{element.TagPrefix}' is registered in DotvvmConfiguration.Markup.Controls collection!";
                element.TagNameNode.AddError($"The control <{element.FullTagName}> could not be resolved!{similarNameHelp}{tagPrefixHelp}");
            }
            if (controlMetadata.VirtualPath is {} && controlMetadata.Type.IsAssignableTo(ResolvedTypeDescriptor.Create(typeof(DotvvmView))))
            {
                element.TagNameNode.AddWarning($"The markup control <{element.FullTagName}> has a baseType DotvvmView. Please make sure that the control file has .dotcontrol file extension. This will work, but causes unexpected issues, for example @js directive will not work in this control.");
            }
            var control = treeBuilder.BuildControl(controlMetadata, element, dataContext);
            control.ConstructorParameters = constructorParameters;

            // resolve data context
            var dataContextAttribute = element.Attributes.FirstOrDefault(a => a.AttributeFullName == "DataContext");
            if (dataContextAttribute != null)
            {
                ProcessAttribute(DotvvmBindableObject.DataContextProperty, dataContextAttribute, control, dataContext);
            }

            if (control.TryGetProperty(DotvvmBindableObject.DataContextProperty, out var dataContextProperty) && dataContextProperty is IAbstractPropertyBinding { Binding: var dataContextBinding })
            {
                var serverOnly = !typeof(IValueBinding).IsAssignableFrom(dataContextBinding.BindingType);
                if (dataContextBinding?.ResultType != null)
                {
                    dataContext = CreateDataContextTypeStack(
                        dataContextBinding.ResultType,
                        parentDataContextStack: dataContext,
                        serverSideOnly: serverOnly
                    );
                }
                else
                {
                    dataContext = CreateDataContextTypeStack(null, dataContext, serverSideOnly: serverOnly);
                }
                control.DataContextTypeStack = dataContext;
            }
            if (controlMetadata.DataContextConstraint != null && dataContext != null && !controlMetadata.DataContextConstraint.IsAssignableFrom(dataContext.DataContextType))
            {
                ((DothtmlNode?)dataContextAttribute ?? element.TagNameNode)
                   .AddError($"The control '{controlMetadata.Type.CSharpName}' requires a DataContext of type '{controlMetadata.DataContextConstraint.CSharpFullName}'!");
            }

            ProcessAttributeProperties(control, element.Attributes.Where(a => a.AttributeFullName != "DataContext").ToArray(), dataContext!);

            // process control contents
            ProcessControlContent(control, element.Content);

            var unknownContent = control.Content.Where(c => !c.Metadata.Type.IsAssignableTo(new ResolvedTypeDescriptor(typeof(DotvvmControl))));
            foreach (var unknownControl in unknownContent)
            {
                unknownControl.DothtmlNode!.AddError($"The control '{ unknownControl.Metadata.Type.CSharpName }' does not inherit from DotvvmControl and thus cannot be used in content.");
            }

            return control;
        }

        /// <summary>
        /// Processes the binding node.
        /// </summary>
        public IAbstractBinding ProcessBinding(DothtmlBindingNode node, IDataContextStack? context, IPropertyDescriptor property)
        {
            var bindingOptions = controlResolver.ResolveBinding(node.Name);
            if (bindingOptions == null)
            {
                node.NameNode.AddError($"Binding {node.Name} could not be resolved.");
                bindingOptions = controlResolver.ResolveBinding("value").NotNull(); // just try it as with value binding
            }

            if (context?.NamespaceImports.Length > 0)
                bindingOptions = bindingOptions.AddImports(context.NamespaceImports);

            return CompileBinding(node, bindingOptions, context!, property);
        }

        static HashSet<string> treatBindingAsHardCodedValue = new HashSet<string> { "resource" };

        private void ProcessAttributeProperties(IAbstractControl control, DothtmlAttributeNode[] nodes, IDataContextStack dataContext)
        {
            var doneAttributes = new HashSet<DothtmlAttributeNode>();
            void resolveAttribute(DothtmlAttributeNode attribute)
            {
                var name = attribute.AttributeFullName;
                if (!doneAttributes.Add(attribute)) return;

                var property = controlResolver.FindProperty(control.Metadata, name, MappingMode.Attribute);
                if (property == null)
                {
                    attribute.AddError($"The control '{control.Metadata.Type}' does not have a property '{attribute.AttributeFullName}' and does not allow HTML attributes!");
                }
                else
                {
                    var dependsOn = property.DataContextChangeAttributes.SelectMany(c => c.PropertyDependsOn);
                    foreach (var p in dependsOn.SelectMany(t => nodes.Where(n => t == n.AttributeFullName)))
                        resolveAttribute(p);
                    ProcessAttribute(property, attribute, control, dataContext);
                }
            }
            // set properties from attributes
            foreach (var attr in nodes)
            {
                resolveAttribute(attr);
            }
        }

        /// <summary>
        /// Processes the attribute node.
        /// </summary>
        private void ProcessAttribute(IPropertyDescriptor property, DothtmlAttributeNode attribute, IAbstractControl control, IDataContextStack dataContext)
        {
            dataContext = GetDataContextChange(dataContext, control, property);

            if (!property.MarkupOptions.MappingMode.HasFlag(MappingMode.Attribute))
            {
                attribute.AddError($"The property '{property.FullName}' cannot be used as a control attribute!");
                return;
            }

            ValidateAttribute(property, control, attribute);

            // set the property
            if (attribute.ValueNode == null)
            {
                // implicitly set boolean property
                if (property.PropertyType.IsEqualTo(new ResolvedTypeDescriptor(typeof(bool))) || property.PropertyType.IsEqualTo(new ResolvedTypeDescriptor(typeof(bool?))))
                {
                    if (!treeBuilder.AddProperty(control, treeBuilder.BuildPropertyValue(property, BoxingUtils.True, attribute), out var error)) attribute.AddError(error);
                }
                else if (property.MarkupOptions.AllowAttributeWithoutValue)
                {
                    if (!treeBuilder.AddProperty(control, treeBuilder.BuildPropertyValue(property, (property as DotVVM.Framework.Binding.DotvvmProperty)?.DefaultValue, attribute), out var error)) attribute.AddError(error);
                }
                else attribute.AddError($"The attribute '{property.Name}' on the control '{control.Metadata.Type.CSharpName}' must have a value!");
            }
            else if (attribute.ValueNode is DothtmlValueBindingNode valueBindingNode)
            {
                // binding
                var bindingNode = valueBindingNode.BindingNode;
                if (!treatBindingAsHardCodedValue.Contains(bindingNode.Name))
                {
                    if (!property.MarkupOptions.AllowBinding)
                        attribute.ValueNode.AddError($"The property '{ property.FullName }' cannot contain {bindingNode.Name} binding.");
                }
                var binding = ProcessBinding(bindingNode, dataContext, property);
                if (property.IsBindingProperty)
                {
                    // check that binding types are compatible
                    if (!property.PropertyType.IsAssignableFrom(ResolvedTypeDescriptor.Create(binding.BindingType)))
                    {
                        attribute.ValueNode.AddError($"The property '{property.FullName}' cannot contain a binding of type '{binding.BindingType}'!");
                    }
                }
                var bindingProperty = treeBuilder.BuildPropertyBinding(property, binding, attribute);
                if (!treeBuilder.AddProperty(control, bindingProperty, out var error)) attribute.AddError(error);
            }
            else
            {
                var textValue = (DothtmlValueTextNode)attribute.ValueNode;

                try
                {
                    // ConvertValue may fail, we don't want to crash the compiler in that case.
                    var value = ConvertValue(WebUtility.HtmlDecode(textValue.Text), property.PropertyType);
                    var propertyValue = treeBuilder.BuildPropertyValue(property, value, attribute);

                    if (!treeBuilder.AddProperty(control, propertyValue, out var error)) attribute.AddError(error);
                }
                catch (Exception e)
                {
                    textValue.AddError($"The value '{textValue.Text}' could not be converted to {property.PropertyType.FullName}: {e.Message}");
                }
            }
        }

        private void ValidateAttribute(IPropertyDescriptor property, IAbstractControl control, DothtmlAttributeNode attribute)
        {
            if (!property.MarkupOptions.AllowHardCodedValue && attribute.ValueNode is not DothtmlValueBindingNode)
            {
                var err = $"The property {property.FullName} cannot contain hard coded value.";
                if (attribute.ValueNode is not null)
                    attribute.ValueNode.AddError(err);
                else
                    attribute.AddError(err);
                return;
            }

            if (!property.MarkupOptions.AllowResourceBinding &&
                attribute.ValueNode is DothtmlValueBindingNode resourceBinding &&
                treatBindingAsHardCodedValue.Contains(resourceBinding.BindingNode.Name))
            {
                // TODO: next major version - make this error
                var err = $"The property {property.FullName} cannot contain hardcoded value nor resource bindings. This will be an error in the next major version.";
                resourceBinding.AddWarning(err);
                return;
            }

            if (!property.MarkupOptions.AllowBinding &&
                attribute.ValueNode is DothtmlValueBindingNode binding &&
                !treatBindingAsHardCodedValue.Contains(binding.BindingNode.Name))
            {
                // stupid edge case, precompiled controls currently don't allow resource bindings, so it's better to not suggest it in the error message
                var allowsResourceBinding =
                    control.Metadata.PrecompilationMode <= ControlPrecompilationMode.IfPossibleAndIgnoreExceptions;
                var resourceBindingHelp =
                    allowsResourceBinding && property.PropertyType.IsPrimitiveTypeDescriptor() ? " You can use a resource binding instead - the resource will evaluate the expression server-side." : "";
                var err = $"The property {property.FullName} cannot contain {binding.BindingNode.Name} binding." + resourceBindingHelp;
                attribute.AddError(err);
                return;
            }

            // validate that html attributes names look valid
            if (property is IGroupedPropertyDescriptor groupedProperty &&
                property.UsedInCapabilities.Any(c => c.PropertyType.IsEqualTo(typeof(HtmlCapability))))
            {
                AddHtmlAttributeWarning(control, attribute, groupedProperty);
            }
        }

        // some SVG attributes contain uppercase letters, we don't want to warn about those
        static HashSet<string> uppercaseHtmlAttributeList = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "attributeName", "baseFrequency", "calcMode", "clipPathUnits", "diffuseConstant", "edgeMode", "edgeMode", "filterUnits", "gradientTransform", "gradientTransform", "gradientUnits", "gradientUnits", "kernelMatrix", "kernelUnitLength", "kernelUnitLength", "kernelUnitLength", "keyPoints", "keySplines", "keyTimes", "lengthAdjust", "limitingConeAngle", "markerHeight", "markerUnits", "markerWidth", "maskContentUnits", "maskUnits", "numOctaves", "pathLength", "patternContentUnits", "patternTransform", "patternUnits", "pointsAtX", "pointsAtY", "pointsAtZ", "preserveAlpha", "preserveAspectRatio", "primitiveUnits", "refX", "refX", "refY", "refY", "repeatCount", "repeatDur", "requiredExtensions", "specularConstant", "specularExponent", "specularExponent", "spreadMethod", "spreadMethod", "startOffset", "stdDeviation", "stdDeviation", "stitchTiles", "surfaceScale", "surfaceScale", "systemLanguage", "tableValues", "targetX", "targetY", "textLength", "textLength", "viewBox", "xChannelSelector", "yChannelSelector", "zoomAndPan" };
        private static void AddHtmlAttributeWarning(IAbstractControl control, DothtmlAttributeNode attribute, IGroupedPropertyDescriptor groupedProperty)
        {
            var pGroup = groupedProperty.PropertyGroup;
            var name = groupedProperty.GroupMemberName;


            var prefix = attribute.AttributeFullName.Substring(0, attribute.AttributeFullName.Length - name.Length);
            // If the HTML attribute is used with a prefix such as `Item`, it might be clearer if the first character is uppercased
            // e.g. ItemClass reads better than Itemclass
            // we supress the warning in such case
            var allowFirstCharacterUppercase = prefix.Length > 0 && char.IsLetter(prefix[prefix.Length - 1]);


            // Ignore SVG attributes (unless they also start with an uppercase letter)
            if ((allowFirstCharacterUppercase || !char.IsUpper(name, 0)) && uppercaseHtmlAttributeList.Contains(name))
                return;

            if (pGroup.Name.EndsWith("Attributes") &&
                name.Substring(allowFirstCharacterUppercase ? 1 : 0).ToLowerInvariant() != name.Substring(allowFirstCharacterUppercase ? 1 : 0))
            {
                // properties with at most two typos
                var similarNameProperties =
                    control.Metadata.AllProperties
                    .Where(p => StringSimilarity.DamerauLevenshteinDistance(p.Name.ToLowerInvariant(), (prefix + name).ToLowerInvariant()) <= 2)
                    // suggest the alias if the property is obsolete
                    .Select(p => p.ObsoleteAttribute is null && p is PropertyAliasAttribute alias ? alias.AliasedPropertyName : p.Name)
                    .ToArray();
                var similarPropertyHelp =
                    similarNameProperties.Any() ? $" Did you mean {string.Join(", ", similarNameProperties)}, or another DotVVM property?" : " Did you intent to use a DotVVM property instead?";
                attribute.AttributeNameNode.AddWarning(
                    $"HTML attribute name '{name}' should not contain uppercase letters." + similarPropertyHelp
                );
            }
        }

        private (string tagPrefix, string name, IControlType)[] FindSimilarControls(string? tagPrefix, string elementName, ITypeDescriptor? controlBaseType = null, int threshold = 4, int limit = 5)
        {
            return (
                from c in this.controlResolver.EnumerateControlTypes()
                where controlBaseType is null || c.type.Type.IsAssignableTo(controlBaseType)
                let prefixScore = tagPrefix is null ? 0 : StringSimilarity.DamerauLevenshteinDistance(c.tagPrefix, tagPrefix)
                where prefixScore <= threshold
                from controlName in Enumerable.Concat([ c.tagName ?? c.type.PrimaryName ], c.type.AlternativeNames)
                let nameScore = StringSimilarity.DamerauLevenshteinDistance(elementName.ToLowerInvariant(), controlName.ToLowerInvariant())
                where prefixScore + nameScore <= threshold
                orderby (prefixScore + nameScore, controlName, c.tagPrefix) descending
                select (c.tagPrefix, controlName, c.type)
            ).Take(limit).ToArray();
        }

        private string? GetMissingElementPropertyDiagnostic(IAbstractControl parentControl, DothtmlElementNode element, ITypeDescriptor? allowedControlTypes = null)
        {
            // * warning: content allowed, but element has uppercase letter
            // * error: content not allowed, no property matches
            // * error: content allowed, HtmlGenericControl isn't allowed, no property matches
            var isUppercase = char.IsUpper(element.TagName, 0);
            var similarNameControls =
                !parentControl.Metadata.IsContentAllowed && parentControl.Metadata.DefaultContentProperty is null
                    ? []
                    : FindSimilarControls(element.TagPrefix, element.TagName, allowedControlTypes);
            var similarNameProperties =
                parentControl.Metadata.AllProperties
                .Where(p => p.MarkupOptions.MappingMode.HasFlag(MappingMode.InnerElement) &&
                            StringSimilarity.DamerauLevenshteinDistance(p.Name.ToLowerInvariant(), element.FullTagName.ToLowerInvariant()) <= 4)
                .Select(p => p.Name)
                .ToArray();
            if (similarNameControls.Any() && similarNameProperties.Any())
            {
                return $" Did you mean property {string.Join(", ", similarNameProperties)}, or control {string.Join(", ", similarNameControls.Select(c => c.tagPrefix + ":" + c.name))}?";
            }
            else if (similarNameProperties.Any())
            {
                return $" Did you mean {string.Join(", ", similarNameProperties)}, or another DotVVM property?";
            }
            else if (similarNameControls.Any())
            {
                return $" Did you mean {string.Join(", ", similarNameControls.Select(c => c.tagPrefix + ":" + c.name))}, or another DotVVM control?";
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Processes the content of the control node.
        /// </summary>
        public void ProcessControlContent(IAbstractControl control, IEnumerable<DothtmlNode> nodes)
        {
            var allowsContent = control.Metadata.IsContentAllowed || control.Metadata.DefaultContentProperty is {};
            var content = new List<DothtmlNode>();
            bool properties = true;
            foreach (var node in nodes)
            {
                var element = node as DothtmlElementNode;
                if (element is {} && properties)
                {
                    var property = controlResolver.FindProperty(control.Metadata, element.TagName, MappingMode.InnerElement);
                    if (property != null && string.IsNullOrEmpty(element.TagPrefix) && property.MarkupOptions.MappingMode.HasFlag(MappingMode.InnerElement))
                    {
                        content.Clear();
                        if (!treeBuilder.AddProperty(control, ProcessElementProperty(control, property, element.Content, element), out var error)) element.AddError(error);

                        foreach (var attr in element.Attributes)
                            attr.AttributeNameNode.AddError("Attributes can't be set on element property.");

                        if (char.IsLower(element.TagName, 0) &&
                            property is not IGroupedPropertyDescriptor &&
                            !property.Name.Equals(element.TagName, StringComparison.Ordinal))
                        {
                            // lowercase inner element property can be easily confused with html elements
                            element.TagNameNode.AddWarning($"The first letter is lowercase, indicating this is an HTML element. However, DotVVM maps the element to property '{property.FullName}'. Please make the first letter uppercase to avoid the ambiguity.");
                        }
                    }
                    else
                    {
                        if (!allowsContent)
                        {
                            var compositeControlHelp =
                                control.Metadata.Type.IsAssignableTo(new ResolvedTypeDescriptor(typeof(CompositeControl))) ?
                                " CompositeControls don't allow content by default and Content or ContentTemplate property is missing on this control." : null;
                            var propertyHelp = GetMissingElementPropertyDiagnostic(control, element, ResolvedTypeDescriptor.Create(typeof(void)));
                            element.TagNameNode.AddError($"Content not allowed inside {control.Metadata.Type.CSharpName}.{propertyHelp}{compositeControlHelp}");
                        }
                        else if (string.IsNullOrEmpty(element.TagPrefix) && char.IsUpper(element.TagName, 0))
                        {
                            var propertyHelp = GetMissingElementPropertyDiagnostic(control, element) ?? "Did you intent to use a DotVVM property or control instead?";
                            element.TagNameNode.AddWarning(
                                $"HTML element name '{element.TagName}' should not contain uppercase letters.{propertyHelp}"
                            );
                        }
                        content.Add(node);
                        if (node.IsNotEmpty())
                        {
                            properties = false;
                        }
                    }
                }
                else
                {
                    content.Add(node);
                    if (node.IsNotEmpty())
                    {
                        // properties = false; // TODO: next major version
                        if (!allowsContent)
                            node.AddError($"Content not allowed inside {control.Metadata.Type.CSharpName}.");

                        // uppercase starting HTML element - warn about properties
                        if (element is {} && string.IsNullOrEmpty(element.TagPrefix) && char.IsUpper(element.TagName, 0))
                        {
                            if (controlResolver.FindProperty(control.Metadata, element.TagName, MappingMode.InnerElement) is {} property)
                            {
                                element.TagNameNode.AddWarning($"This element looks like an inner element property {property.FullName}, but it isn't, because it is prefixed by other content ('{content.FirstOrDefault(c => c.IsNotEmpty())}')).");
                            }
                            else
                            {
                                element.TagNameNode.AddWarning(
                                    $"HTML element name '{element.TagName}' should not contain uppercase letters.{GetMissingElementPropertyDiagnostic(control, element)}"
                                );
                            }
                        }
                    }
                }
            }
            if (control.Metadata.DefaultContentProperty is IPropertyDescriptor contentProperty)
            {
                // don't assign the property, when content is empty
                if (content.All(c => c.IsEmpty()))
                    return;

                if (control.HasProperty(contentProperty))
                {
                    foreach (var c in content)
                        if (c.IsNotEmpty())
                            c.AddError($"Property { contentProperty.FullName } was already set.");
                }
                else
                {
                    if (!treeBuilder.AddProperty(control, ProcessElementProperty(control, contentProperty, content, null), out var error))
                        content.First().AddError(error);
                }
            }
            else
            {
                if (control.Metadata.IsContentAllowed)
                {
                    ResolveControlContentImmediately(control, content);
                }
            }
        }

        /// <summary>
        /// Resolves the content of the control immediately during the parsing.
        /// Override this method if you need lazy loading of tree node contents.
        /// </summary>
        protected virtual void ResolveControlContentImmediately(IAbstractControl control, List<DothtmlNode> content)
        {
            var dataContext = GetDataContextChange(control.DataContextTypeStack, control, null);
            foreach (var node in content)
            {
                var child = ProcessNode(control, node, control.Metadata, dataContext);
                if (child != null)
                {
                    treeBuilder.AddChildControl(control, child);
                    if (!child.Metadata.Type.IsAssignableTo(ResolvedTypeDescriptor.Create(typeof(DotvvmControl))))
                        node.AddError($"Content control must inherit from DotvvmControl, but {child.Metadata.Type} doesn't.");
                }
            }
        }

        /// <summary>
        /// Processes the element which contains property value.
        /// </summary>
        private IAbstractPropertySetter ProcessElementProperty(IAbstractControl control, IPropertyDescriptor property, IEnumerable<DothtmlNode> elementContent, DothtmlElementNode? propertyWrapperElement)
        {
            IEnumerable<IAbstractControl> filterByType(ITypeDescriptor type, IEnumerable<IAbstractControl> controls) =>
                FilterOrError(controls,
                        c => c is object && c.Metadata.Type.IsAssignableTo(type),
                        c => {
                            // empty nodes are only filtered out, non-empty nodes cause errors
                            if (c.DothtmlNode.IsNotEmpty())
                            {
                                // when used without explicit property wrapper element, add information about available content properties
                                var propertyHelp = propertyWrapperElement is null && c.DothtmlNode is DothtmlElementNode element ? GetMissingElementPropertyDiagnostic(control, element, type) : null;
                                ((c.DothtmlNode as DothtmlElementNode)?.TagNameNode ?? c.DothtmlNode)
                                    .AddError($"Control type {c.Metadata.Type.CSharpFullName} can't be used in a property of type {type.CSharpFullName}.{propertyHelp}");
                            }
                        });

            // resolve data context
            var dataContext = control.DataContextTypeStack;
            dataContext = GetDataContextChange(dataContext, control, property);

            // the element is a property
            if (IsTemplateProperty(property))
            {
                // template
                return treeBuilder.BuildPropertyTemplate(property, ProcessTemplate(control, elementContent, dataContext), propertyWrapperElement);
            }
            else if (property.PropertyType.IsEqualTo(new ResolvedTypeDescriptor(typeof(string))))
            {
                // string property
                var strings = FilterNodes<DothtmlLiteralNode>(elementContent, property);
                var value = string.Concat(strings.Select(s => s.Value));
                return treeBuilder.BuildPropertyValue(property, value, propertyWrapperElement);
            }
            else if (IsControlProperty(property))
            {
                var children = filterByType(property.PropertyType, elementContent.Select(childObject => ProcessNode(control, childObject, control.Metadata, dataContext)!)).ToArray();
                if (children.Length > 1)
                {
                    // try with the empty nodes are excluded
                    children = children.Where(c => c.DothtmlNode.IsNotEmpty()).ToArray();
                    if (children.Length > 1)
                    {
                        foreach (var c in children.Skip(1))
                            c.DothtmlNode!.AddError($"The property '{property.MarkupOptions.Name}' can have only one child element!");
                    }
                }
                if (children.Length >= 1)
                {
                    return treeBuilder.BuildPropertyControl(property, children[0], propertyWrapperElement);
                }
                else
                {
                    return treeBuilder.BuildPropertyControl(property, null, propertyWrapperElement);
                }
            }
            else if (IsCollectionProperty(property))
            {
                var collectionType = GetCollectionType(property);
                    
                // collection of elements
                var collection = elementContent.Select(childObject => ProcessNode(control, childObject, control.Metadata, dataContext)!);
                if (collectionType is null)
                {
                    control.DothtmlNode!.AddError($"The property '{property.FullName}' is a collection, but the collection type could not be determined.");
                }
                else
                {
                    if (!collectionType.IsAssignableTo(ResolvedTypeDescriptor.Create(typeof(IDotvvmObjectLike))))
                        control.DothtmlNode!.AddError($"The property '{property.FullName}' of type '{property.PropertyType.CSharpName}' cannot be used as an inner element. It is not a collection of DotvvmControl or IDotvvmObjectLike.");

                    collection = filterByType(collectionType, collection);
                }

                return treeBuilder.BuildPropertyControlCollection(property, collection.ToArray(), propertyWrapperElement);
            }
            else
            {
                control.DothtmlNode!.AddError($"The property '{property.FullName}' cannot be used as an inner element. The type '{property.PropertyType.CSharpName}' is not supported.");
                return treeBuilder.BuildPropertyValue(property, null, propertyWrapperElement);
            }
        }

        /// <summary>
        /// Processes the template contents.
        /// </summary>
        private List<IAbstractControl> ProcessTemplate(IAbstractTreeNode parent, IEnumerable<DothtmlNode> elementContent, IDataContextStack dataContext)
        {
            var content = elementContent.Select(e => ProcessNode(parent, e, placeholderMetadata.Value, dataContext)!).Where(e => e != null);
            return content.ToList();
        }

        protected IEnumerable<T> FilterOrError<T>(IEnumerable<T> controls, Predicate<T> predicate, Action<T> errorAction)
        {
            foreach (var item in controls)
            {
                if (predicate(item)) yield return item;
                else errorAction(item);
            }
        }

        /// <summary>
        /// Gets the inner property elements and makes sure that no other content is present.
        /// </summary>
        private IEnumerable<TNode> FilterNodes<TNode>(IEnumerable<DothtmlNode> nodes, IPropertyDescriptor property) where TNode : DothtmlNode
        {
            foreach (var child in nodes)
            {
                if (child is TNode)
                {
                    yield return (TNode)child;
                }
                else if (child.IsNotEmpty())
                {
                    child.AddError($"Content is not allowed inside the property '{property.FullName}'! (Conflicting node: Node {child.GetType().Name})");
                }
            }
        }

        protected virtual bool IsCollectionProperty(IPropertyDescriptor property)
        {
            return property.PropertyType.IsAssignableTo(new ResolvedTypeDescriptor(typeof(IEnumerable)));
        }

        protected virtual ITypeDescriptor? GetCollectionType(IPropertyDescriptor property)
        {
            return property.PropertyType.TryGetArrayElementOrIEnumerableType();
        }


        protected virtual bool IsTemplateProperty(IPropertyDescriptor property)
        {
            return property.PropertyType.IsAssignableTo(new ResolvedTypeDescriptor(typeof(ITemplate)));
        }

        protected virtual bool IsControlProperty(IPropertyDescriptor property)
        {
            return property.PropertyType.IsAssignableTo(new ResolvedTypeDescriptor(typeof(DotvvmControl)));
        }


        /// <summary>
        /// Gets the data context change behavior for the specified control property.
        /// </summary>
        [return: NotNullIfNotNull("dataContext")]
        protected virtual IDataContextStack? GetDataContextChange(IDataContextStack dataContext, IAbstractControl control, IPropertyDescriptor? property)
        {
            if (dataContext == null) return null;

            var manipulationAttribute = property != null ? property.DataContextManipulationAttribute : control.Metadata.DataContextManipulationAttribute;
            if (manipulationAttribute != null)
            {
                return manipulationAttribute.ChangeStackForChildren(dataContext, control, property!, (parent, changeType) => CreateDataContextTypeStack(changeType, parentDataContextStack: parent));
            }

            var attributes = property != null ? property.DataContextChangeAttributes : control.Metadata.DataContextChangeAttributes;
            if (attributes == null || attributes.Length == 0) return dataContext;

            try
            {
                var (type, extensionParameters, addLayer, serverOnly) = ApplyContextChange(dataContext, attributes, control, property);

                if (type == null) return dataContext;
                else if (!addLayer) return CreateDataContextTypeStack(dataContext.DataContextType, dataContext.Parent, dataContext.NamespaceImports, extensionParameters.Concat(dataContext.ExtensionParameters).ToArray());
                else return CreateDataContextTypeStack(type, parentDataContextStack: dataContext, extensionParameters: extensionParameters.ToArray(), serverSideOnly: serverOnly);
            }
            catch (Exception exception)
            {
                var node = property != null && control.TryGetProperty(property, out var v) ? v.DothtmlNode : control.DothtmlNode;
                node?.AddError($"Could not compute the type of DataContext: {exception}");

                return CreateDataContextTypeStack(null, parentDataContextStack: dataContext);
            }
        }

        public static (ITypeDescriptor? type, List<BindingExtensionParameter> extensionParameters, bool addLayer, bool serverOnly) ApplyContextChange(IDataContextStack dataContext, DataContextChangeAttribute[] attributes, IAbstractControl control, IPropertyDescriptor? property)
        {
            var serverOnly = dataContext.ServerSideOnly;
            var type = dataContext.DataContextType;
            var extensionParameters = new List<BindingExtensionParameter>();
            var addLayer = false;
            foreach (var attribute in attributes.OrderBy(a => a.Order))
            {
                if (type == null) break;
                extensionParameters.AddRange(attribute.GetExtensionParameters(type));
                if (attribute.NestDataContext)
                {
                    addLayer = true;
                    type = attribute.GetChildDataContextType(type, dataContext, control, property);
                    serverOnly = attribute.IsServerSideOnly(dataContext, control, property) ?? serverOnly;
                }
            }
            return (type, extensionParameters, addLayer, serverOnly);
        }


        /// <summary>
        /// Creates the IControlType identification of the control.
        /// </summary>
        protected abstract IControlType CreateControlType(ITypeDescriptor wrapperType, string virtualPath);

        /// <summary>
        /// Creates the data context type stack object.
        /// </summary>
        protected abstract IDataContextStack CreateDataContextTypeStack(ITypeDescriptor? viewModelType, IDataContextStack? parentDataContextStack = null, IReadOnlyList<NamespaceImport>? imports = null, IReadOnlyList<BindingExtensionParameter>? extensionParameters = null, bool serverSideOnly = false);

        /// <summary>
        /// Converts the value to the property type.
        /// </summary>
        protected abstract object? ConvertValue(string value, ITypeDescriptor propertyType);

        /// <summary>
        /// Compiles the binding.
        /// </summary>
        protected abstract IAbstractBinding CompileBinding(DothtmlBindingNode node, BindingParserOptions bindingOptions, IDataContextStack context, IPropertyDescriptor property);

    }
}
