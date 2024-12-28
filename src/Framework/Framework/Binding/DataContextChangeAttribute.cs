using DotVVM.Framework.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using System.ComponentModel;

namespace DotVVM.Framework.Binding
{
    /// Controls which data context should be used inside of the marked control or property
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = true)]
    public abstract class DataContextChangeAttribute : Attribute
    {
        /// When there is multiple of these attributes, they are executed in order which is determined by this parameter
        public abstract int Order { get; }

        /// Returns a the data context type that should be inside of the annotated control/property.
        /// Returning null means that the data context should not be changed. This overload is used by the view compiler.
        public abstract ITypeDescriptor? GetChildDataContextType(ITypeDescriptor dataContext, IDataContextStack controlContextStack, IAbstractControl control, IPropertyDescriptor? property = null);

        /// Returns a the data context type that should be inside of the annotated control/property.
        /// Returning null means that the data context should not be changed. This overload is used at runtime, by `DotvvmProperty.GetDataContextType(DotvvmBindableObject)` helper method.
        public abstract Type? GetChildDataContextType(Type dataContext, DataContextStack controlContextStack, DotvvmBindableObject control, DotvvmProperty? property = null);

        /// Whether new layer of DataContext should be created, or the current one should be adjusted (extension parameters will be added).
        [DefaultValue(true)]
        public virtual bool NestDataContext => true;

        /// Gets the extension parameters that should be made available to the bindings inside.
        public virtual IEnumerable<BindingExtensionParameter> GetExtensionParameters(ITypeDescriptor dataContext) => Enumerable.Empty<BindingExtensionParameter>();

        /// <summary> Gets if the nested data context is available only on server or also client-side, controls the <see cref="DataContextStack.ServerSideOnly" /> property. `null` means that it should be inherited from the parent context. </summary>
        public virtual bool? IsServerSideOnly(IDataContextStack controlContextStack, IAbstractControl control, IPropertyDescriptor? property = null) => null;
        /// <summary> Gets if the nested data context is available only on server or also client-side, controls the <see cref="DataContextStack.ServerSideOnly" /> property. `null` means that it should be inherited from the parent context. </summary>
        public virtual bool? IsServerSideOnly(DataContextStack controlContextStack, DotvvmBindableObject control, DotvvmProperty? property = null) => null;

        /// Gets a list of attributes that need to be resolved before this attribute is invoked.
        public virtual IEnumerable<string> PropertyDependsOn => Enumerable.Empty<string>();
    }
}
