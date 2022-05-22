using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Content of this control is displayed if and only if DataSource is empty or null
    /// </summary>
    public class EmptyData : ItemsControl
    {
        /// <summary>
        /// Gets or sets the name of the tag that wraps the Repeater.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string WrapperTagName
        {
            get { return (string)GetValue(WrapperTagNameProperty)!; }
            set { SetValue(WrapperTagNameProperty, value ?? throw new ArgumentNullException(nameof(value))); }
        }

        public static readonly DotvvmProperty WrapperTagNameProperty =
            DotvvmProperty.Register<string, EmptyData>(t => t.WrapperTagName, "div");

        /// <summary>
        /// Gets or sets whether the control should render a wrapper element.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool RenderWrapperTag
        {
            get { return (bool)GetValue(RenderWrapperTagProperty)!; }
            set { SetValue(RenderWrapperTagProperty, value); }
        }

        public static readonly DotvvmProperty RenderWrapperTagProperty =
            DotvvmProperty.Register<bool, EmptyData>(t => t.RenderWrapperTag, true);

        protected override bool RendersHtmlTag => RenderWrapperTag;

        public EmptyData()
        {
        }

        protected override void RenderControl(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var dataSourceBinding = GetValueBinding(DataSourceProperty);
            TagName = WrapperTagName;
            // if DataSource is resource binding && DataSource is not empty then don't render anything
            if (dataSourceBinding is {} || GetIEnumerableFromDataSource()?.GetEnumerator()?.MoveNext() != true)
            {
                if (dataSourceBinding is {})
                {
                    var visibleBinding =
                        dataSourceBinding
                        .GetProperty<DataSourceLengthBinding>().Binding
                        .GetProperty<IsMoreThanZeroBindingProperty>().Binding
                        .GetProperty<NegatedBindingExpression>().Binding
                        .CastTo<IValueBinding>();
                    this.AndAssignProperty(IncludeInPageProperty, visibleBinding);
                }

                base.RenderControl(writer, context);
            }
        }
    }
}
