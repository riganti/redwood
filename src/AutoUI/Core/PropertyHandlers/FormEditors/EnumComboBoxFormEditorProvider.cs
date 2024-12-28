using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using DotVVM.AutoUI.Controls;
using DotVVM.AutoUI.Metadata;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;
using Humanizer;

namespace DotVVM.AutoUI.PropertyHandlers.FormEditors
{
    public class EnumComboBoxFormEditorProvider : FormEditorProviderBase
    {
        public override bool CanHandleProperty(PropertyDisplayMetadata property, AutoUIContext context)
        {
            return property.Type.UnwrapNullableType().IsEnum;
        }

        public override DotvvmControl CreateControl(PropertyDisplayMetadata property, AutoEditor.Props props, AutoUIContext context)
        {
            var enumType = property.Type.UnwrapNullableType();
            var isNullable = property.Type.IsNullable();

            var options = Enum.GetNames(enumType)
                .Select(name => {
                    var displayAttribute = enumType
                        .GetField(name)
                        ?.GetCustomAttribute<DisplayAttribute>();
                    var displayName =
                        LocalizableString.CreateNullable(displayAttribute?.Name, displayAttribute?.ResourceType) ??
                        LocalizableString.Constant(name.Humanize());
                    var title = LocalizableString.CreateNullable(displayAttribute?.Description, displayAttribute?.ResourceType);
                    var enumJsString = ReflectionUtils.ToEnumString(enumType, name);

                    return new SelectorItem(displayName.ToBinding(context.BindingService), new(enumJsString))
                                .AddAttribute("title", title?.ToBinding(context.BindingService));
                });

            var control = new ComboBox()
                .SetCapability(props.Html)
                .SetProperty(c => c.Enabled, props.Enabled)
                .SetProperty(c => c.SelectionChanged, props.Changed)
                .SetProperty(c => c.SelectedValue, props.Property);

            if (isNullable)
            {
                control.AppendChildren(new SelectorItem(property.NullDisplayText ?? "---", null!));
            }

            return control
                .AppendChildren(options);
        }
    }
}
