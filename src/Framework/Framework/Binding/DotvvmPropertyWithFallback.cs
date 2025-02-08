using System;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Binding
{
    /// <summary>
    /// The DotvvmProperty that fallbacks to another DotvvmProperty's value.
    /// </summary>
    public sealed class DotvvmPropertyWithFallback : DotvvmProperty
    {
        /// <summary>
        /// Gets the property which value will be used as a fallback when this property is not set.
        /// </summary>
        public DotvvmProperty FallbackProperty { get; private set; }

        public DotvvmPropertyWithFallback(DotvvmProperty fallbackProperty, string name, Type declaringType, bool isValueInherited): base(name, declaringType, isValueInherited)
        {
            this.FallbackProperty = fallbackProperty;
        }

        /// <inheritdoc />
        public override object? GetValue(DotvvmBindableObject control, bool inherit = true)
        {
            if (!TryGetValue(control, out var value, inherit))
            {
                return FallbackProperty.GetValue(control, inherit);
            }

            return value;
        }

        /// <summary>
        /// Gets whether the value of the property is set
        /// </summary>
        public override bool IsSet(DotvvmBindableObject control, bool inherit = true)
        {
            return base.IsSet(control, inherit) || FallbackProperty.IsSet(control, inherit);
        }

        /// <summary>
        /// Registers a new DotVVM property which fallbacks to the <paramref name="fallbackProperty" /> when not set.
        /// </summary>
        /// <param name="propertyAccessor">The expression pointing to instance property.</param>
        /// <param name="fallbackProperty">The property which value will be used as a fallback when the new property is not set.</param>
        /// <param name="isValueInherited">Indicates whether the value can be inherited from the parent controls.</param>
        public static DotvvmProperty Register<TPropertyType, TDeclaringType>(Expression<Func<TDeclaringType, object>> propertyAccessor, DotvvmProperty fallbackProperty, bool isValueInherited = false)
        {
            var property = ReflectionUtils.GetMemberFromExpression(propertyAccessor) as PropertyInfo;
            if (property == null) throw new ArgumentException("The expression should be simple property access", nameof(propertyAccessor));
            return Register<TPropertyType, TDeclaringType>(property.Name, fallbackProperty, isValueInherited);
        }

        /// <summary>
        /// Registers a new DotVVM property which fallbacks to the <paramref name="fallbackProperty" /> when not set.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="fallbackProperty">The property which value will be used as a fallback when the new property is not set.</param>
        /// <param name="isValueInherited">Indicates whether the value can be inherited from the parent controls.</param>
        public static DotvvmPropertyWithFallback Register<TPropertyType, TDeclaringType>(string propertyName, DotvvmProperty fallbackProperty, bool isValueInherited = false)
        {
            var property = new DotvvmPropertyWithFallback(fallbackProperty, propertyName, typeof(TDeclaringType), isValueInherited: isValueInherited);
            Register<TPropertyType, TDeclaringType>(propertyName, isValueInherited: isValueInherited, property: property);
            property.DefaultValue = fallbackProperty.DefaultValue;
            return property;
        }

        private bool TryGetValue(DotvvmBindableObject control, out object? value, bool inherit = true)
        {
            if (control.properties.TryGet(this, out value))
            {
                return true;
            }

            if (IsValueInherited && inherit && control.Parent != null)
            {
                return TryGetValue(control.Parent, out value);
            }

            value = null;
            return false;
        }
    }
}
