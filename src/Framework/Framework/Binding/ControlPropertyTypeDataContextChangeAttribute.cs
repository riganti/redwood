using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Controls;

namespace DotVVM.Framework.Binding
{
    [Obsolete("Use ControlPropertyBindingDataContextChangeAttribute instead.")]
    public class ControlPropertyTypeDataContextChangeAttribute : DataContextChangeAttribute
    {
        public string PropertyName { get; set; }

        public override int Order { get; }

        public ControlPropertyTypeDataContextChangeAttribute(string propertyName, int order = 0)
        {
            PropertyName = propertyName;
            Order = order;
        }

        public override ITypeDescriptor? GetChildDataContextType(ITypeDescriptor dataContext, IDataContextStack controlContextStack, IAbstractControl control, IPropertyDescriptor? property = null)
        {
            if (!control.Metadata.TryGetProperty(PropertyName, out var controlProperty))
            {
                throw new Exception($"The property '{PropertyName}' was not found on control '{control.Metadata.Type}'!");
            }

            if (control.TryGetProperty(controlProperty, out var setter) && setter is IAbstractPropertyBinding binding)
            {
                return binding.Binding.ResultType;
            }

            return controlProperty.PropertyType;
        }

        public override Type? GetChildDataContextType(Type dataContext, DataContextStack controlContextStack, DotvvmBindableObject control, DotvvmProperty? property = null)
        {
            var controlType = control.GetType();
            var controlPropertyField = controlType.GetField($"{PropertyName}Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var controlProperty = (DotvvmProperty?)controlPropertyField?.GetValue(null);

            if (controlProperty == null)
            {
                throw new Exception($"The property '{PropertyName}' was not found on control '{controlType}'!");
            }

            if (control.properties.Contains(controlProperty) && control.GetBinding(controlProperty) is IStaticValueBinding valueBinding)
            {
                return valueBinding.ResultType;
            }

            return controlProperty.PropertyType;
        }
    }
}
