using DotVVM.Framework.Controls;
using DotVVM.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Binding
{
    /// <summary> An abstract DotvvmProperty which contains code to be executed when the assigned control is being rendered. </summary>
    public abstract class ActiveDotvvmProperty : DotvvmProperty
    {
        internal ActiveDotvvmProperty(string name, Type declaringType, bool isValueInherited) : base(name, declaringType, isValueInherited)
        {
        }

        public abstract void AddAttributesToRender(IHtmlWriter writer, IDotvvmRequestContext context, DotvvmControl control);


        public static ActiveDotvvmProperty RegisterCommandToAttribute<TDeclaringType>(string name, string attributeName)
        {
            return DelegateActionProperty<ICommandBinding>.Register<TDeclaringType>(name, (writer, context, property, control) => {
                var value = control.GetValueRaw(property);
                if (value is null)
                {
                }
                else if (value is ICommandBinding binding)
                {
                    var script = KnockoutHelper.GenerateClientPostBackScript(name, binding, control);
                    writer.AddAttribute(attributeName, script);
                }
                else
                {
                    throw new DotvvmControlException(control, $"Command binding expression was expected in {property}, got {value} instead.");
                }
            });
        }
    }
}
