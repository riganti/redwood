using System.Collections.Generic;
using System.Collections.Immutable;

namespace DotVVM.Framework.Compilation.ControlTree
{
    public interface IDataContextStack
    {
        ITypeDescriptor DataContextType { get; }

        IDataContextStack? Parent { get; }

        ImmutableArray<NamespaceImport> NamespaceImports { get; }

        ImmutableArray<BindingExtensionParameter> ExtensionParameters { get; }
        IEnumerable<(int dataContextLevel, BindingExtensionParameter parameter)> GetCurrentExtensionParameters();

        bool ServerSideOnly { get; }

    }
}
