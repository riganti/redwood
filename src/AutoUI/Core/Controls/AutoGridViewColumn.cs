using System;
using System.Linq;
using DotVVM.AutoUI.PropertyHandlers;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;

namespace DotVVM.AutoUI.Controls
{
    [ControlMarkupOptions(PrimaryName = "GridViewColumn")]
    public class AutoGridViewColumn : GridViewColumn
    {
        [MarkupOptions(AllowHardCodedValue = false, AllowResourceBinding = true, Required = true)]
        public IStaticValueBinding? Property
        {
            get { return (IStaticValueBinding?)GetValue(PropertyProperty); }
            set { SetValue(PropertyProperty, value); }
        }
        public static readonly DotvvmProperty PropertyProperty =
            DotvvmProperty.Register<IStaticValueBinding, AutoGridViewColumn>(nameof(Property));


        public static DotvvmCapabilityProperty PropsProperty =
            DotvvmCapabilityProperty.RegisterCapability<Props, AutoGridViewColumn>();

        public static GridViewColumn Replace(IStyleMatchContext<AutoGridViewColumn> col)
        {
            var context = new AutoUIContext(col.Control.DataContextTypeStack, col.Configuration.ServiceProvider);

            var props = col.PropertyValue<Props>(PropsProperty).NotNull();
            if (props.Property is null)
                throw new DotvvmControlException($"AutoGridViewColumn.Property is not set.");


            var prop = props.Property.GetProperty<ReferencedViewModelPropertiesBindingProperty>();

            if (prop.MainProperty is null)
                throw new NotSupportedException($"The binding {props.Property} must be bound to a single property. Alternatively, you can write a custom server-side style rule for your expression.");

            var propertyMetadata = context.PropertyDisplayMetadataProvider.GetPropertyMetadata(prop.MainProperty);

            if (props.HeaderTemplate is null && props.HeaderText is null)
            {
                props = props with { HeaderText = propertyMetadata.GetDisplayName().ToBinding(context.BindingService) };
            }

            var control = CreateColumn(context, props, propertyMetadata);
            if (!control.IsPropertySet(HeaderTextProperty) && !control.IsPropertySet(HeaderTemplateProperty))
            {
                control.SetValue(HeaderTextProperty, props.HeaderText);
            }

            // editor

            if (props.EditTemplate is null && (props.IsEditable.HasBinding || props.IsEditable.ValueOrDefault == true))
            {
                control.EditTemplate = new CloneTemplate(
                    AutoEditor.Build(new AutoEditor.Props()
                    {
                        Property = props.Property,
                        Changed = props.Changed,
                        Enabled = props.IsEditable
                    }, context)
                );
            }
            else
                control.EditTemplate = props.EditTemplate;

            return control;

        }

        private static GridViewColumn CreateColumn(AutoUIContext context, Props props, Metadata.PropertyDisplayMetadata property)
        {
            if (props.ContentTemplate is { })
                return new GridViewTemplateColumn { ContentTemplate = props.ContentTemplate };

            var provider = context.AutoUiConfiguration.GridColumnProviders.FindBestProvider(property, context);
            if (provider is null)
                throw new DotvvmControlException($"GridViewColumn provider for property {property.Name} or type {property.Type} could not be found.");

            var control = provider.CreateColumn(property, props, context);
            return control;
        }

        public override void CreateControls(IDotvvmRequestContext context, DotvvmControl container) => throw new NotImplementedException("AutoGridViewColumn must be replaced using server-side styles. It cannot be used at runtime");
        public override void CreateEditControls(IDotvvmRequestContext context, DotvvmControl container) => throw new NotImplementedException("AutoGridViewColumn must be replaced using server-side styles. It cannot be used at runtime");

        [DotvvmControlCapability]
        public sealed record Props
        {
            public IStaticValueBinding? Property { get; init; }
            public ValueOrBinding<bool> IsEditable { get; init; } = new(true);
            public ValueOrBinding<string>? HeaderText { get; init; }
            public ITemplate? HeaderTemplate { get; init; }
            public ITemplate? EditTemplate { get; init; }
            public ITemplate? ContentTemplate { get; init; }

            public ICommandBinding? Changed { get; init; }
        }
    }
}
