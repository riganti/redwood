using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Controls;

namespace DotVVM.Framework.Binding
{
    /// <summary> Changes the data context type to the type specified in the attribute constructor. </summary>
    public class ConstantDataContextChangeAttribute : DataContextChangeAttribute
    {
        public Type Type { get; }

        public override int Order { get; }
        public bool? ServerSideOnly { get; }

        public ConstantDataContextChangeAttribute(Type type, int order = 0, bool? serverSideOnly = null)
        {
            Type = type;
            Order = order;
            ServerSideOnly = serverSideOnly;
        }
        
        public override ITypeDescriptor? GetChildDataContextType(ITypeDescriptor dataContext, IDataContextStack controlContextStack, IAbstractControl control, IPropertyDescriptor? property = null)
        {
            return new ResolvedTypeDescriptor(Type);
        }

        public override Type? GetChildDataContextType(Type dataContext, DataContextStack controlContextStack, DotvvmBindableObject control, DotvvmProperty? property = null)
        {
            return Type;
        }

        public override bool? IsServerSideOnly(IDataContextStack controlContextStack, IAbstractControl control, IPropertyDescriptor? property = null) =>
            ServerSideOnly;
        public override bool? IsServerSideOnly(DataContextStack controlContextStack, DotvvmBindableObject control, DotvvmProperty? property = null) =>
            ServerSideOnly;
    }
}
