using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls.DynamicData
{
    public static class DataContextStackHelper
    {
        public static DataContextStack GetItemDataContextStack(this DotvvmBindableObject bindableObject, DotvvmProperty dataSourceProperty)
        {
            return bindableObject.GetValueBinding(dataSourceProperty)
                ?.GetProperty<CollectionElementDataContextBindingProperty>()?.DataContext;
        }

        public static DataContextStack CreateChildStack(this DotvvmBindableObject bindableObject, Type viewModelType)
        {
            var dataContextTypeStack = bindableObject.GetDataContextType();

            return DataContextStack.Create(
                viewModelType,
                dataContextTypeStack,
                dataContextTypeStack.NamespaceImports,
                dataContextTypeStack.ExtensionParameters,
                dataContextTypeStack.BindingPropertyResolvers);
        }

        public static DataContextStack CreateChildStack(this DataContextStack dataContextStack, Type viewModelType)
        {
            return DataContextStack.Create(
                viewModelType,
                dataContextStack,
                dataContextStack.NamespaceImports,
                dataContextStack.ExtensionParameters,
                dataContextStack.BindingPropertyResolvers);
        }
    }
}
