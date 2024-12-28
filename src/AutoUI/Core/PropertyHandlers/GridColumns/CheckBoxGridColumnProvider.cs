using DotVVM.AutoUI.Controls;
using DotVVM.AutoUI.Metadata;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;

namespace DotVVM.AutoUI.PropertyHandlers.GridColumns
{
    public class CheckBoxGridColumnProvider : GridColumnProviderBase
    {
        public override bool CanHandleProperty(PropertyDisplayMetadata property, AutoUIContext context)
        {
            return property.Type.UnwrapNullableType() == typeof(bool);
        }

        protected override GridViewColumn CreateColumnCore(PropertyDisplayMetadata property, AutoGridViewColumn.Props props, AutoUIContext context)
        {
            var column = new GridViewCheckBoxColumn();
            column.SetBinding(GridViewCheckBoxColumn.ValueBindingProperty, props.Property);
            return column;
        }
    }
}
