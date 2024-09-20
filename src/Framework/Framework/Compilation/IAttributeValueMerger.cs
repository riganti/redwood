using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Compilation
{
    public interface IAttributeValueMerger
    {
        ResolvedPropertySetter? MergeResolvedValues(ResolvedPropertySetter a, ResolvedPropertySetter b, out string? error);
        object? MergePlainValues(DotvvmPropertyId prop, object? a, object? b);
    }
}
