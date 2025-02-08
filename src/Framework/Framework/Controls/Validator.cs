using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Binding.Properties;
using System.Text.Json;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Displays the asterisk or validation message for a specified field.
    /// </summary>
    public class Validator : HtmlGenericControl
    {
        /// <summary>
        /// Gets or sets whether the control should be hidden even for valid values.
        /// </summary>
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty HideWhenValidProperty =
            DotvvmProperty.Register<bool, Validator>(() => HideWhenValidProperty, isValueInherited: true);

        /// <summary>
        /// Gets or sets the name of CSS class which is applied to the control when it is not valid.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        [AttachedProperty(typeof(string))]
        public static readonly DotvvmProperty InvalidCssClassProperty =
            DotvvmProperty.Register<string, Validator>(() => InvalidCssClassProperty, isValueInherited: true);

        /// <summary>
        /// Gets or sets whether the title attribute should be set to the error message.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty SetToolTipTextProperty =
            DotvvmProperty.Register<bool, Validator>(() => SetToolTipTextProperty, isValueInherited: true);

        /// <summary>
        /// Gets or sets whether the error message text should be displayed.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        [AttachedProperty(typeof(bool))]
        public static readonly DotvvmProperty ShowErrorMessageTextProperty =
            DotvvmProperty.Register<bool, Validator>(() => ShowErrorMessageTextProperty, isValueInherited: true);


        /// <summary>
        /// Gets or sets a binding that points to the validated value.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false)]
        [AttachedProperty(typeof(object))]
        public static readonly ActiveDotvvmProperty ValueProperty =
            DelegateActionProperty<object>.Register<Validator>("Value", AddValidatedValue);

        public static List<DotvvmProperty> ValidationOptionProperties { get; } = new List<DotvvmProperty>()
        {
            HideWhenValidProperty,
            InvalidCssClassProperty,
            SetToolTipTextProperty,
            ShowErrorMessageTextProperty
        };

        public static void Place(
            IDotvvmControl control,
            DotvvmControlCollection container,
            IValueBinding? value,
            ValidatorPlacement placement)
        {
            if (value is null)
                return;

            if (placement.HasFlag(ValidatorPlacement.AttachToControl)) {
                control.SetValue(ValueProperty, value!);
            }
            if (placement.HasFlag(ValidatorPlacement.Standalone)) {
                var validator = new Validator();
                validator.SetValue(ValueProperty, value);
                container.Add(validator);
            }
        }

        private static void AddValidatedValue(IHtmlWriter writer, IDotvvmRequestContext context, DotvvmProperty prop, DotvvmControl control)
        {
            const string validationDataBindName = "dotvvm-validation";

            var binding = control.GetValueBinding(ValueProperty);
            if (binding is not null)
            {
                var referencedPropertyExpressions = binding.GetProperty<ReferencedViewModelPropertiesBindingProperty>();
                var unwrappedPropertyExpression = referencedPropertyExpressions.UnwrappedBindingExpression.NotNull();

                // We were able to unwrap the the provided expression
                writer.AddKnockoutDataBind(validationDataBindName, control, unwrappedPropertyExpression);
            }
            else
            {
                // Note: ValueProperty can sometimes contain null (BusinessPack depends on this behaviour)
                // However, it certainly should not contain hard-coded values

                var valueRaw = control.GetValueRaw(ValueProperty);
                if (valueRaw != null)
                {
                    // There is a hard-coded value in the ValueProperty
                    throw new DotvvmControlException($"{nameof(ValueProperty)} can not contain a hard-coded value.");
                }
            }

            // render options
            var bindingGroup = new KnockoutBindingGroup();
            foreach (var property in ValidationOptionProperties)
            {
                var javascriptName = KnockoutHelper.ConvertToCamelCase(property.Name);
                var optionValue = control.GetValue(property);
                if (!object.Equals(optionValue, property.DefaultValue))
                {
                    var settings = DefaultSerializerSettingsProvider.Instance.SettingsHtmlUnsafe;
                    bindingGroup.Add(javascriptName, JsonSerializer.Serialize(optionValue, settings));
                }
            }
            writer.AddKnockoutDataBind("dotvvm-validationOptions", bindingGroup);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            TagName = "span";
            SetValue(HideWhenValidProperty, true);
        }
    }
}
