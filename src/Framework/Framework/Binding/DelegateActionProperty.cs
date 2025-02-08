using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Binding
{
    /// <summary> DotvvmProperty which calls the function passed in the Register method, when the assigned control is being rendered. </summary>
    public sealed class DelegateActionProperty<TValue>: ActiveDotvvmProperty
    {
        private readonly Action<IHtmlWriter, IDotvvmRequestContext, DotvvmProperty, DotvvmControl> func;

        public DelegateActionProperty(Action<IHtmlWriter, IDotvvmRequestContext, DotvvmProperty, DotvvmControl> func, string name, Type declaringType, bool isValueInherited) : base(name, declaringType, isValueInherited)
        {
            this.func = func;
        }

        public override void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context, DotvvmControl control)
        {
            if(func != null) func(writer, context, this, control);
        }

        public static DelegateActionProperty<TValue> Register<TDeclaringType>(string name, Action<IHtmlWriter, IDotvvmRequestContext, DotvvmProperty, DotvvmControl> func, [AllowNull] TValue defaultValue = default(TValue))
        {
            var property = new DelegateActionProperty<TValue>(func, name, typeof(TDeclaringType), isValueInherited: false);
            return (DelegateActionProperty<TValue>)DotvvmProperty.Register<TValue, TDeclaringType>(name, defaultValue, false, property);
        }

    }
}
