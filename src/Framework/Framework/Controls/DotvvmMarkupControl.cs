using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Parser;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.ViewModel.Serialization;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A base class for all controls with Dothtml markup.
    /// </summary>
    public class DotvvmMarkupControl : HtmlGenericControl
    {
        public Dictionary<string, string> Directives { get; } = new Dictionary<string, string>();


        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmMarkupControl"/> class.
        /// </summary>
        public DotvvmMarkupControl() : this("div")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmMarkupControl"/> class.
        /// </summary>
        public DotvvmMarkupControl(string? wrapperTagName) : base(wrapperTagName, false)
        {
            LifecycleRequirements |= ControlLifecycleRequirements.PreInit;
            SetValue(Internal.IsNamingContainerProperty, true);
            SetValue(Internal.IsControlBindingTargetProperty, true);
        }

        internal override void OnPreInit(IDotvvmRequestContext context)
        {
            if (Directives.ContainsKey(ParserConstants.WrapperTagNameDirective) && Directives.ContainsKey(ParserConstants.NoWrapperTagNameDirective))
            {
                throw new DotvvmControlException(this, $"Control cannot have {ParserConstants.WrapperTagNameDirective} and {ParserConstants.NoWrapperTagNameDirective} at the same time");
            }

            if (Directives.TryGetValue(ParserConstants.WrapperTagNameDirective, out var wrapperTagName))
            {
                TagName = wrapperTagName;
            }
            else if (Directives.ContainsKey(ParserConstants.NoWrapperTagNameDirective))
            {
                TagName = null;
            }

            var viewModule = GetValue<ViewModuleReferenceInfo>(Internal.ReferencedViewModuleInfoProperty);
            if (viewModule is {})
            {
                Debug.Assert(viewModule.IsMarkupControl);
                context.ResourceManager.AddRequiredResource(viewModule.ImportResourceName);
            }

            base.OnPreInit(context);
        }

        bool closeKoComment = false;

        protected override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var properties = new KnockoutBindingGroup();
            var usedProperties = GetValue<ControlUsedPropertiesInfo>(Internal.UsedPropertiesInfoProperty);

            if (usedProperties?.UsesViewModelClientSide == true)
            {
                // check that the markup control isn't used in resource-binding only data context
                var dataContext = this.GetDataContextType();
                if (dataContext?.ServerSideOnly == true)
                {
                    throw new DotvvmControlException(this, $"Markup control '{GetType().Name}' cannot be used in resource-binding only data context, because it uses value bindings on the data context.");
                }
            }

            foreach (var p in usedProperties?.ClientSideUsedProperties ?? GetDeclaredProperties())
            {
                if (p.DeclaringType.IsAssignableFrom(typeof(DotvvmMarkupControl)))
                    continue;

                var pinfo = GetPropertySerializationInfo(p); // migrate to use the KnockoutBindingGroup helpers
                if (pinfo.Js is {})
                {
                    properties.Add(p.Name, pinfo.Js);
                }
            }

            foreach (var pg in usedProperties?.ClientSideUsedPropertyGroups ?? DotvvmPropertyGroup.GetPropertyGroups(this.GetType()))
            {
                if (pg.DeclaringType.IsAssignableFrom(typeof(DotvvmMarkupControl)))
                    continue;

                var js = new StringBuilder().Append("[");
                
                var values = new VirtualPropertyGroupDictionary<object>(this, pg);
                foreach (var p in values.Properties.OrderBy(p => p.GroupMemberName))
                {
                    var pinfo = GetPropertySerializationInfo(p); // migrate to use the KnockoutBindingGroup helpers
                    if (pinfo.Js is {})
                    {
                        js.Append("{Key: ")
                          .Append(KnockoutHelper.MakeStringLiteral(p.GroupMemberName, htmlSafe: true))
                          .Append(", Value: ")
                          .Append(pinfo.Js)
                          .Append("},");
                    }
                }
                js.Append("]");
                properties.Add(pg.Name, js.ToString());
            }


            var koComment = new List<string>();
            if (!properties.IsEmpty)
            {
                if (RendersHtmlTag)
                    writer.AddKnockoutDataBind("dotvvm-with-control-properties", properties);
                else
                {
                    koComment.Add("dotvvm-with-control-properties: " + properties.ToString());
                }
            }

            var viewModule = this.GetValue<ViewModuleReferenceInfo>(Internal.ReferencedViewModuleInfoProperty);
            if (viewModule is {})
            {
                var binding = $"{{ modules: {JsonSerializer.Serialize(viewModule.ReferencedModules, DefaultSerializerSettingsProvider.Instance.Settings)} }}";
                if (RendersHtmlTag)
                    writer.AddKnockoutDataBind("dotvvm-with-view-modules", binding);
                else
                {
                    koComment.Add("dotvvm-with-view-modules: " + binding);
                }
            }

            if (closeKoComment = koComment.Count > 0)
            {
                // the WriteKnockoutDataBindEndComment doesn't support multiple comments in one element, which is needed for viewmodules and properties working properly together
                writer.WriteUnencodedText("<!-- ko ");
                writer.WriteUnencodedText(string.Join(", ", koComment));
                writer.WriteUnencodedText(" -->");
            }


            base.AddAttributesToRender(writer, context);
        }

        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            base.RenderEndTag(writer, context);

            if (closeKoComment)
            {
                writer.WriteKnockoutDataBindEndComment();
            }
        }

        private PropertySerializeInfo GetPropertySerializationInfo(DotvvmProperty property)
        {
            if (ContainsPropertyStaticValue(property))
            {
                return new PropertySerializeInfo(
                    property,
                    JsonSerializer.Serialize(GetValue(property), DefaultSerializerSettingsProvider.Instance.Settings)
                );
            }
            else if (GetBinding(property) is IValueBinding valueBinding)
            {
                return new PropertySerializeInfo(
                    property,
                    valueBinding.GetKnockoutBindingExpression(this)
                );
            }
            else if (GetBinding(property) is ICommandBinding command)
            {

                return new PropertySerializeInfo(
                    property,
                    KnockoutHelper.GenerateClientPostbackLambda(
                        property.Name,
                        command,
                        this
                    )
                );
            }          
            else
            {
                return new PropertySerializeInfo(property, null);
            }
        }

        private bool ContainsPropertyStaticValue(DotvvmProperty property)
        {
            var binding = GetBinding(property);

            var isValueOrServerSideValueBinding = binding == null || (binding is IStaticValueBinding && !(binding is IValueBinding));

            return isValueOrServerSideValueBinding && !typeof(ITemplate).IsAssignableFrom(property.PropertyType);
        }

        private class PropertySerializeInfo
        {
            public PropertySerializeInfo(DotvvmProperty property, string? js)
            {
                Js = js;
                Property = property;
            }
            public string? Js { get; set; }
            public DotvvmProperty Property { get; set; }
        }
    }
}
