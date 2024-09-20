using System;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Controls;

namespace DotVVM.Framework.Binding
{
    public sealed class DotvvmPropertyAlias : DotvvmProperty
    {
        private DotvvmProperty? aliased;

        public DotvvmPropertyAlias(
            string aliasName,
            Type declaringType,
            string aliasedPropertyName,
            Type aliasedPropertyDeclaringType,
            System.Reflection.ICustomAttributeProvider attributeProvider): base(aliasName, declaringType, isValueInherited: false)
        {
            AliasedPropertyName = aliasedPropertyName;
            AliasedPropertyDeclaringType = aliasedPropertyDeclaringType;
            MarkupOptions = new MarkupOptionsAttribute();
            DataContextChangeAttributes = Array.Empty<DataContextChangeAttribute>();
        }

        public string AliasedPropertyName { get; }
        public Type AliasedPropertyDeclaringType { get; }
        public DotvvmProperty Aliased
        {
            get
            {
                if (aliased == null)
                {
                    throw new NotSupportedException($"The '{FullName}' property alias has " +
                        "not been resolved yet.");
                }
                return aliased;
            }
        }
        public bool IsResolved => aliased is object;

        public static void Resolve(DotvvmPropertyAlias alias)
        {
            var aliased = DotvvmProperty.ResolveProperty(
                alias.AliasedPropertyDeclaringType,
                alias.AliasedPropertyName);
            if (aliased is null)
            {
                throw new ArgumentException($"Property alias '{alias}' could not be resolved. "
                    + $"The aliased property '{alias.AliasedPropertyDeclaringType.Name}.{alias.AliasedPropertyName}' "
                    + "is not registered.");
            }

            alias.aliased = aliased;

            // NB: this property copying is required for the dothtml compiler to resolve the property correctly
            //     before the alias can be applied
            alias.DefaultValue = aliased.DefaultValue;
            alias.PropertyType = aliased.PropertyType;
            alias.IsValueInherited = aliased.IsValueInherited;
            alias.MarkupOptions = aliased.MarkupOptions;
            alias.IsBindingProperty = aliased.IsBindingProperty;
            alias.DataContextChangeAttributes = aliased.DataContextChangeAttributes;
            alias.DataContextManipulationAttribute = aliased.DataContextManipulationAttribute;
        }

        public override object? GetValue(DotvvmBindableObject control, bool inherit = true)
        {
            throw GetException();
        }

        public override bool IsSet(DotvvmBindableObject control, bool inherit = true)
        {
            throw GetException();
        }

        public override void SetValue(DotvvmBindableObject control, object? value)
        {
            throw GetException();
        }

        private Exception GetException([CallerMemberName] string member = "<missing>")
        {
            return new NotSupportedException($"'{FullName}' is a property alias and doesn't support "
                + $"'{member}'. Use '{Aliased.FullName}' instead.");
        }
    }
}
