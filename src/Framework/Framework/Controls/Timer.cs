using System;
using System.Collections.Generic;
using System.Text;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// An invisible control that periodically invokes a command.
    /// </summary>
    public class Timer : DotvvmControl
    {
        /// <summary>
        /// Gets or sets the command binding that will be invoked on every tick.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false, Required = true)]
        public ICommandBinding Command
        {
            get { return (ICommandBinding)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }
        public static readonly DotvvmProperty CommandProperty
            = DotvvmProperty.Register<ICommandBinding, Timer>(c => c.Command, null);

        /// <summary>
        /// Gets or sets the interval in milliseconds.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public int Interval
        {
            get { return (int)GetValue(IntervalProperty); }
            set { SetValue(IntervalProperty, value); }
        }
        public static readonly DotvvmProperty IntervalProperty
            = DotvvmProperty.Register<int, Timer>(c => c.Interval, 30000);

        /// <summary>
        /// Gets or sets whether the timer is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return (bool)GetValue(EnabledProperty); }
            set { SetValue(EnabledProperty, value); }
        }
        public static readonly DotvvmProperty EnabledProperty
            = DotvvmProperty.Register<bool, Timer>(c => c.Enabled, true);

        public Timer()
        {
            SetValue(Validation.EnabledProperty, false);
            SetValue(PostBack.ConcurrencyProperty, PostbackConcurrencyMode.Queue);
        }

        protected override void RenderBeginTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            var group = new KnockoutBindingGroup();
            group.Add("command", KnockoutHelper.GenerateClientPostbackLambda("Command", Command, this));
            group.Add("interval", Interval.ToString());
            group.Add("enabled", this, EnabledProperty);
            writer.WriteKnockoutDataBindComment("timer", group.ToString());

            base.RenderBeginTag(writer, context);
        }

        protected override void RenderEndTag(IHtmlWriter writer, IDotvvmRequestContext context)
        {
            base.RenderEndTag(writer, context);

            writer.WriteKnockoutDataBindEndComment();
        }
    }
}
