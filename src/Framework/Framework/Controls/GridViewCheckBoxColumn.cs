using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A GridView column which renders a bool value and can edit it in the CheckBox control.
    /// </summary>
    [ControlMarkupOptions(AllowContent = false)]
    public class GridViewCheckBoxColumn : GridViewColumn
    {
        /// <summary>
        /// Gets or sets a binding which retrieves the value to display from the current data item.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false, Required = true)]
        public IStaticValueBinding ValueBinding
        {
            get { return (IStaticValueBinding)GetValueRaw(ValueBindingProperty)!; }
            set { SetValue(ValueBindingProperty, value); }
        }
        public static readonly DotvvmProperty ValueBindingProperty =
            DotvvmProperty.Register<IStaticValueBinding<bool?>, GridViewCheckBoxColumn>(c => c.ValueBinding);

        /// <summary> Whether to automatically attach Validator.Value onto the TextBox or add a standalone Validator component. </summary>
        public ValidatorPlacement ValidatorPlacement
        {
            get { return (ValidatorPlacement)GetValue(ValidatorPlacementProperty)!; }
            set { SetValue(ValidatorPlacementProperty, value); }
        }
        public static readonly DotvvmProperty ValidatorPlacementProperty
            = DotvvmProperty.Register<ValidatorPlacement, GridViewCheckBoxColumn>(c => c.ValidatorPlacement, default);

        public override void CreateControls(IDotvvmRequestContext context, DotvvmControl container)
        {
            CreateControlsCore(container, enabled: false);
        }

        public override void CreateEditControls(IDotvvmRequestContext context, DotvvmControl container)
        {
            if (EditTemplate is {} editTemplate)
            {
                editTemplate.BuildContent(context, container);
                return;
            }
            CreateControlsCore(container, enabled: true);
        }

        private void CreateControlsCore(DotvvmControl container, bool enabled)
        {
            var checkBox = new CheckBox { Enabled = enabled };
            CopyProperty(UITests.NameProperty, checkBox, UITests.NameProperty);

            var binding = ValueBinding;
            checkBox.SetBinding(CheckBox.CheckedProperty, binding);
            Validator.Place(checkBox, container.Children, binding as IValueBinding, ValidatorPlacement);
            container.Children.Add(checkBox);
        }

        protected override string? GetSortExpression()
        {
            if (string.IsNullOrEmpty(SortExpression))
            {
                return GetBinding(ValueBindingProperty)?.GetProperty<OriginalStringBindingProperty>()?.Code ??
                       throw new DotvvmControlException(this, $"The 'ValueBinding' property must be set on the '{GetType()}' control!");
            }
            else
            {
                return SortExpression;
            }
        }
    }
}
