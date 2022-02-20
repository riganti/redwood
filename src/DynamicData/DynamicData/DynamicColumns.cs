using System;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.Styles;
using DotVVM.Framework.Controls.DynamicData.Metadata;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls.DynamicData
{
    // TODO: replace this with something else
    public class DummyColumnThatDoesNothing : GridViewColumn
    {
        public DummyColumnThatDoesNothing()
        {
            Visible = false;
        }

        public override void CreateControls(IDotvvmRequestContext context, DotvvmControl container) { }
        public override void CreateEditControls(IDotvvmRequestContext context, DotvvmControl container) { }
    }
    public class DynamicColumns: GridViewColumn
    {
        public static DotvvmCapabilityProperty PropsProperty =
            DotvvmCapabilityProperty.RegisterCapability<Props, DynamicColumns>();

        public static GridViewColumn[] Replace(IStyleMatchContext<DynamicColumns> col)
        {
            if (col.HasProperty(c => c.EditTemplate))
                throw new NotSupportedException("EditTemplate is not supported in DynamicGridColumnGroup.");

            var props = col.PropertyValue<Props>(PropsProperty).NotNull();

            var context = new DynamicDataContext(col.Control.DataContextTypeStack, col.Configuration.ServiceProvider)
            {
                ViewName = props.FieldSelector.ViewName,
                GroupName = props.FieldSelector.GroupName
            };

            var properties = DynamicEntityBase.GetPropertiesToDisplay(context, props.FieldSelector);

            var columns = properties.Select(p => CreateColumn(p, context, props)).ToArray();
            return columns;
        }

        protected static DynamicGridColumn CreateColumn(PropertyDisplayMetadata property, DynamicDataContext context, Props props)
        {
            return
                new DynamicGridColumn()
                    .SetProperty(p => p.Property, context.CreateValueBinding(property));
                // .SetProperty("Changed", props.Changed.GetValueOrDefault(property.PropertyInfo.Name))
                // .SetProperty("Enabled", props.Enabled.GetValueOrDefault(property.PropertyInfo.Name, true));
        }

        public override void CreateControls(IDotvvmRequestContext context, DotvvmControl container) => throw new NotImplementedException("DynamicGridColumn must be replaced using server-side styles. It cannot be used at runtime");
        public override void CreateEditControls(IDotvvmRequestContext context, DotvvmControl container) => throw new NotImplementedException("DynamicGridColumn must be replaced using server-side styles. It cannot be used at runtime");

        [DotvvmControlCapability]
        public sealed record Props
        {
            public IValueBinding? Property { get; init; }
            public ValueOrBinding<bool> IsEditable { get; init; } = new(true);

            public DynamicEntityBase.FieldSelectorProps FieldSelector { get; init; } = new();
        }
    }
}
