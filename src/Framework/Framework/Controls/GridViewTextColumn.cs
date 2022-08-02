using DotVVM.Framework.Binding;
using DotVVM.Framework.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A GridView column which renders a text value (with formatting support) and can edit it in the TextBox control.
    /// </summary>
    [ControlMarkupOptions(AllowContent = false)]
    public class GridViewTextColumn : GridViewColumn
    {
        /// <summary>
        /// Gets or sets the format string that will be applied to numeric or date-time values.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string? FormatString
        {
            get { return (string?)GetValue(FormatStringProperty); }
            set { SetValue(FormatStringProperty, value); }
        }
        public static readonly DotvvmProperty FormatStringProperty
            = DotvvmProperty.Register<string?, GridViewTextColumn>(c => c.FormatString, null);

        /// <summary>
        /// Gets or sets the command that will be triggered when the control text is changed.
        /// </summary>
        public ICommandBinding? ChangedBinding
        {
            get { return (ICommandBinding?)GetValue(ChangedBindingProperty); }
            set { SetValue(ChangedBindingProperty, value); }
        }
        public static readonly DotvvmProperty ChangedBindingProperty =
            DotvvmProperty.Register<ICommandBinding?, GridViewTextColumn>(t => t.ChangedBinding, null);

        /// <summary>
        /// Gets or sets a binding which retrieves the value to display from the current data item.
        /// </summary>
        [MarkupOptions(Required = true)]
        public IStaticValueBinding? ValueBinding
        {
            get { return (IStaticValueBinding?)GetBinding(ValueBindingProperty); }
            set { SetValue(ValueBindingProperty, value); }
        }
        public static readonly DotvvmProperty ValueBindingProperty =
            DotvvmProperty.Register<IStaticValueBinding?, GridViewTextColumn>(c => c.ValueBinding);

        /// <summary> Whether to automatically attach Validator.Value onto the TextBox or add a standalone Validator component. </summary>
        [MarkupOptions(AllowBinding = false)]
        public ValidatorPlacement ValidatorPlacement
        {
            get { return (ValidatorPlacement)GetValue(ValidatorPlacementProperty)!; }
            set { SetValue(ValidatorPlacementProperty, value); }
        }
        public static readonly DotvvmProperty ValidatorPlacementProperty
            = DotvvmProperty.Register<ValidatorPlacement, GridViewTextColumn>(c => c.ValidatorPlacement, default);

        protected override string? GetSortExpression()
        {
            if (string.IsNullOrEmpty(SortExpression))
            {
                return ValueBinding?.GetProperty<OriginalStringBindingProperty>()?.Code ??
                    throw new DotvvmControlException(this, $"The 'ValueBinding' property must be set on the '{GetType()}' control!");
            }
            else
            {
                return SortExpression;
            }
        }

        public override void CreateControls(IDotvvmRequestContext context, DotvvmControl container)
        {
            var binding = ValueBinding;
            if (binding is null)
                throw new DotvvmControlException(this, "The 'ValueBinding' property is required.");

            var literal = new Literal();

            CopyPropertyRaw(FormatStringProperty, literal, Literal.FormatStringProperty);
            CopyPropertyRaw(UITests.NameProperty, literal, UITests.NameProperty);

            literal.SetBinding(Literal.TextProperty, binding);
            if (binding is IValueBinding v)
                Validator.Place(literal, container.Children, v, ValidatorPlacement);
            container.Children.Add(literal);
        }

        public override void CreateEditControls(IDotvvmRequestContext context, DotvvmControl container)
        {
            if (EditTemplate is {} editTemplate)
            {
                editTemplate.BuildContent(context, container);
                return;
            }

            var textBox = new TextBox();

            CopyPropertyRaw(FormatStringProperty, textBox, TextBox.FormatStringProperty);
            textBox.SetBinding(TextBox.TextProperty, ValueBinding);
            CopyPropertyRaw(ChangedBindingProperty, textBox, TextBox.ChangedProperty);
            if (ValueBinding is IValueBinding v)
                Validator.Place(textBox, container.Children, v, ValidatorPlacement);
            container.Children.Add(textBox);
        }
    }
}
