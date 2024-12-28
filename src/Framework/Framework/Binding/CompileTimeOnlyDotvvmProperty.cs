using System;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Binding
{
    /// <summary>
    /// The DotvvmProperty that can only be used at compile time (in server-side styles or precompiled CompositeControls)
    /// </summary>
    public class CompileTimeOnlyDotvvmProperty : DotvvmProperty
    {
        private CompileTimeOnlyDotvvmProperty(string name, Type declaringType) : base(name, declaringType, isValueInherited: false)
        {
        }

        public override object? GetValue(DotvvmBindableObject control, bool inherit = true)
        {
            throw new NotSupportedException($"Property {FullName} cannot be accessed, it shall only be used at compile time.");
        }

        public override void SetValue(DotvvmBindableObject control, object? value)
        {

            throw new NotSupportedException($"Property {FullName} cannot be assigned, it shall only be used at compile time.");
        }


        public override bool IsSet(DotvvmBindableObject control, bool inherit = true)
        {
            return false;
        }

        /// <summary>
        /// Registers a new DotVVM property which can only be used at compile time.
        /// </summary>
        public static CompileTimeOnlyDotvvmProperty Register<TPropertyType, TDeclaringType>(string propertyName)
        {
            var property = new CompileTimeOnlyDotvvmProperty(propertyName, typeof(TDeclaringType));
            return (CompileTimeOnlyDotvvmProperty)Register<TPropertyType, TDeclaringType>(propertyName, property: property);
        }
    }
}
