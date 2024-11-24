using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Compilation.Validation;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Renders the HTML checkbox control.
    /// </summary>
    public class CheckBox : CheckableControlBase
    {
        /// <summary>
        /// Gets or sets whether the control is checked.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false)]
        public bool? Checked
        {
            get { return (bool?)GetValue(CheckedProperty); }
            set { SetValue(CheckedProperty, value); }
        }

        public static readonly DotvvmProperty CheckedProperty =
            DotvvmProperty.Register<bool?, CheckBox>(t => t.Checked, null);

        /// <summary>
        /// Gets or sets a collection of values of all checked checkboxes. Use this property in combination with the CheckedValue property.
        /// </summary>
        public IEnumerable? CheckedItems
        {
            get { return (IEnumerable?)GetValue(CheckedItemsProperty); }
            set { SetValue(CheckedItemsProperty, value); }
        }
        public static readonly DotvvmProperty CheckedItemsProperty =
            DotvvmProperty.Register<IEnumerable?, CheckBox>(t => t.CheckedItems, null);


        /// <summary> When set to true, `null` in the Checked property will be treated as `false`, instead of marking the checkbox as indeterminate </summary>
        [MarkupOptions(AllowBinding = true)]
        public bool DisableIndeterminate
        {
            get { return (bool)GetValue(DisableIndeterminateProperty)!; }
            set { SetValue(DisableIndeterminateProperty, value); }
        }
        public static readonly DotvvmProperty DisableIndeterminateProperty =
            DotvvmProperty.Register<bool, CheckBox>(nameof(DisableIndeterminate));


        /// <summary>
        /// Renders the input tag.
        /// </summary>
        protected override void RenderInputTag(IHtmlWriter writer)
        {
            var checkedValue = GetValueRaw(CheckedProperty);
            var checkedItemsValue = GetValueRaw(CheckedItemsProperty);
            if (checkedValue is {} && checkedItemsValue is null)
            {
                // boolean mode
                RenderCheckedProperty(writer, checkedValue);
            }
            else if (checkedValue is null && checkedItemsValue is {})
            {
                // collection mode
                RenderCheckedItemsProperty(writer);
            }
            else
            {
                throw new DotvvmControlException(this, "Either the Checked or the CheckedItems binding of a CheckBox must be set.");
            }

            RenderTypeAttribute(writer);
            writer.RenderSelfClosingTag("input");
        }

        protected virtual void RenderTypeAttribute(IHtmlWriter writer)
        {
            // render the input tag
            writer.AddAttribute("type", "checkbox");
        }

        protected virtual void RenderCheckedItemsProperty(IHtmlWriter writer)
        {
            RenderCheckedItemsBinding(writer);
            writer.AddKnockoutDataBind("checkedArrayContainsObservables", "true");
            writer.AddKnockoutDataBind("dotvvm-checkbox-updateAfterPostback", "true");
            RenderDotvvmCheckedPointerBinding(writer);
            writer.AddKnockoutDataBind("checkedValue", this, CheckedValueProperty, () => {
                var checkedValue = (CheckedValue ?? string.Empty).ToString();
                if (!string.IsNullOrEmpty(checkedValue))
                {
                    writer.AddKnockoutDataBind("checkedValue", KnockoutHelper.MakeStringLiteral(checkedValue));
                }
            });
            RenderCheckedValueComparerAttribute(writer);
        }

        protected virtual void RenderDotvvmCheckedPointerBinding(IHtmlWriter writer)
        {
            writer.AddKnockoutDataBind("dotvvm-checked-pointer", GetDotvvmCheckedPointerBindingValue());
        }

        protected virtual string GetDotvvmCheckedPointerBindingValue()
        {
            if (HasValueBinding(CheckedItemsProperty))
            {
                return "'dotvvm-checkedItems'";
            }
            return "'checked'";
        }

        protected virtual void RenderCheckedItemsBinding(IHtmlWriter writer)
        {
            var checkedItemsBinding = GetValueBinding(CheckedItemsProperty);
            writer.AddKnockoutDataBind("dotvvm-checkedItems", checkedItemsBinding!.GetKnockoutBindingExpression(this));
        }

        protected virtual void RenderCheckedProperty(IHtmlWriter writer, object? checkedValue)
        {
            if (checkedValue is IValueBinding checkedBinding)
            {
                // dotvvm-CheckState sets elements to indeterminate state when checkedBinding is null,
                // knockout's default checked binding does not do that
                var bindingHandler = DisableIndeterminate ? "checked" : "dotvvm-CheckState";
                writer.AddKnockoutDataBind(bindingHandler, checkedBinding!, this);

                // Boolean mode can have prerendered `checked` attribute
                if (RenderOnServer && KnockoutHelper.TryEvaluateValueBinding(this, checkedBinding) is true)
                    writer.AddAttribute("checked", null);
            }
            else
            {
                if ((bool?)EvalPropertyValue(CheckedProperty, checkedValue) is true)
                {
                    writer.AddAttribute("checked", null);
                }
            }
        }


        [ControlUsageValidator]
        public new static IEnumerable<ControlUsageError> ValidateUsage(ResolvedControl control)
        {
            var to = control.GetValue(CheckedItemsProperty)?.GetResultType()?.UnwrapNullableType()?.GetEnumerableType();
            var nonNullableTo = to?.UnwrapNullableType();
            var from = control.GetValue(CheckedValueProperty)?.GetResultType();

            if (to != null && from != null
                && !to.IsAssignableFrom(from) && !nonNullableTo!.IsAssignableFrom(from))
            {
                yield return new ControlUsageError(
                    $"Type of items in CheckedItems \'{to}\' must be same as CheckedValue type \'{from}\'.",
                    control.GetValue(CheckedItemsProperty)?.DothtmlNode,
                    control.GetValue(CheckedValueProperty)?.DothtmlNode
                );
            }

            if (control.HasProperty(DisableIndeterminateProperty) && control.HasProperty(CheckedItemsProperty))
            {
                yield return new ControlUsageError(
                    $"The DisableIndeterminate property has no effect when CheckedItems collection is used.",
                    control.GetValue(DisableIndeterminateProperty)?.DothtmlNode ?? control.DothtmlNode
                );
            }
        }

    }
}
