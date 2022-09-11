using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Utils;
using FastExpressionCompiler;

namespace DotVVM.Framework.Compilation.ControlTree
{
    /// <summary>
    /// Represents compile-time DataContext info - Type of current DataContext, it's parent and other available parameters
    /// </summary>
    [HandleAsImmutableObjectInDotvvmPropertyAttribute]
    public sealed class DataContextStack : IDataContextStack
    {
        public DataContextStack? Parent { get; }
        /// <summary> Type of `_this` </summary>
        public Type DataContextType { get; }
        /// <summary> Namespaces imported by data context change attributes. </summary>
        public ImmutableArray<NamespaceImport> NamespaceImports { get; }
        /// <summary> Extension parameters added by data context change attributes (for example _index, _collection). </summary>
        public ImmutableArray<BindingExtensionParameter> ExtensionParameters { get; }
        /// <summary> Extension property resolvers added by data context change attributes. </summary>
        public ImmutableArray<Delegate> BindingPropertyResolvers { get; }
        /// <summary> When true, this data context is not available client-side, because `DataContext={resource: ...}` was used in the markup. Only resource and command bindings can use this data context. </summary>
        public bool ServerSideOnly { get; }

        private readonly int hashCode;
        private DataContextStack(Type type,
            DataContextStack? parent = null,
            IReadOnlyList<NamespaceImport>? imports = null,
            IReadOnlyList<BindingExtensionParameter>? extensionParameters = null,
            IReadOnlyList<Delegate>? bindingPropertyResolvers = null,
            bool serverSideOnly = false)
        {
            Parent = parent;
            DataContextType = type;
            NamespaceImports = imports?.ToImmutableArray() ?? parent?.NamespaceImports ?? ImmutableArray<NamespaceImport>.Empty;
            ExtensionParameters = extensionParameters?.ToImmutableArray() ?? ImmutableArray<BindingExtensionParameter>.Empty;
            BindingPropertyResolvers = bindingPropertyResolvers?.ToImmutableArray() ?? ImmutableArray<Delegate>.Empty;
            ServerSideOnly = serverSideOnly;

            hashCode = ComputeHashCode();
        }

        /// <summary>
        /// Gets all extension parameter available in current context and their definition offset
        /// </summary>
        public IEnumerable<(int dataContextLevel, BindingExtensionParameter parameter)> GetCurrentExtensionParameters()
        {
            var blackList = new HashSet<string>();
            var current = this;
            int level = 0;
            while (current != null)
            {
                foreach (var p in current.ExtensionParameters.Where(p => blackList.Add(p.Identifier) && (current == this || p.Inherit)))
                    yield return (level, p);
                current = current.Parent;
                level++;
            }
        }

        public ImmutableArray<Delegate> GetAllBindingPropertyResolvers()
        {
            var builder = ImmutableArray.CreateBuilder<Delegate>();

            var c = this;
            while (c is {})
            {
                builder.AddRange(c.BindingPropertyResolvers);
                c = c.Parent;
            }

            builder.Reverse();
            return builder.ToImmutable();
        }


        public IEnumerable<DataContextStack> EnumerableItems()
        {
            var c = this;
            while (c != null)
            {
                yield return c;
                c = c.Parent;
            }
        }

        public IEnumerable<Type> Enumerable()
        {
            var c = this;
            while (c != null)
            {
                yield return c.DataContextType;
                c = c.Parent;
            }
        }

        public IEnumerable<Type> Parents()
        {
            var c = Parent;
            while (c != null)
            {
                yield return c.DataContextType;
                c = c.Parent;
            }
        }

        public bool IsAncestorOf(DataContextStack x)
        {
            var c = x.Parent;
            while (c != null)
            {
                if (this.hashCode == c.hashCode)
                {
                    if (this.Equals(c))
                        return true;
                }
                c = c.Parent;
            }
            return false;
        }

        ITypeDescriptor IDataContextStack.DataContextType => new ResolvedTypeDescriptor(DataContextType);
        IDataContextStack? IDataContextStack.Parent => Parent;

        public override bool Equals(object? obj) =>
            obj is DataContextStack other && Equals(other);

        public bool Equals(DataContextStack? stack)
        {
            return ReferenceEquals(this, stack) || stack is not null
                && hashCode == stack.hashCode
                && DataContextType == stack.DataContextType
                && NamespaceImports.SequenceEqual(stack.NamespaceImports)
                && ExtensionParameters.SequenceEqual(stack.ExtensionParameters)
                && Equals(Parent, stack.Parent);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        int ComputeHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                foreach (var import in NamespaceImports)
                {
                    hashCode += (hashCode * 47) ^ import.GetHashCode();
                }

                foreach (var parameter in ExtensionParameters)
                {
                    hashCode *= 17;
                    hashCode += parameter.GetHashCode();
                }

                return (hashCode, Parent, DataContextType?.FullName, ServerSideOnly).GetHashCode();
            }
        }

        public override string ToString()
        {
            string?[] features = new [] {
                $"type={this.DataContextType.ToCode()}",
                this.ServerSideOnly ? "server-side-only" : null,
                this.NamespaceImports.Any() ? "imports=[" + string.Join(", ", this.NamespaceImports) + "]" : null,
                this.ExtensionParameters.Any() ? "ext=[" + string.Join(", ", this.ExtensionParameters.Select(e => e.Identifier + ": " + e.ParameterType.CSharpName)) + "]" : null,
                this.BindingPropertyResolvers.Any() ? "resolvers=[" + string.Join(", ", this.BindingPropertyResolvers.Select(s => s.Method)) + "]" : null,
                this.Parent != null ? "par=[" + string.Join(", ", this.Parents().Select(p => p.ToCode(stripNamespace: true))) + "]" : null
            };
            return "(" + features.Where(a => a != null).StringJoin(", ") + ")";
        }


        //private static ConditionalWeakTable<DataContextStack, DataContextStack> internCache = new ConditionalWeakTable<DataContextStack, DataContextStack>();
        public static DataContextStack Create(Type type,
            DataContextStack? parent = null,
            IReadOnlyList<NamespaceImport>? imports = null,
            IReadOnlyList<BindingExtensionParameter>? extensionParameters = null,
            IReadOnlyList<Delegate>? bindingPropertyResolvers = null,
            bool serverSideOnly = false)
        {
            var dcs = new DataContextStack(type, parent, imports, extensionParameters, bindingPropertyResolvers, serverSideOnly);
            return dcs;// internCache.GetValue(dcs, _ => dcs);
        }


        /// <summary> Creates a new data context level with _index and _collection extension parameters. </summary>
        public static DataContextStack CreateCollectionElement(Type elementType,
            DataContextStack? parent = null,
            IReadOnlyList<NamespaceImport>? imports = null,
            IReadOnlyList<BindingExtensionParameter>? extensionParameters = null,
            IReadOnlyList<Delegate>? bindingPropertyResolvers = null,
            bool serverSideOnly = false)
        {
            var indexParameters = new CollectionElementDataContextChangeAttribute(0).GetExtensionParameters(new ResolvedTypeDescriptor(elementType.MakeArrayType()));
            extensionParameters = extensionParameters is null ? indexParameters.ToArray() : extensionParameters.Concat(indexParameters).ToArray();
            return DataContextStack.Create(
                elementType, parent,
                imports: imports,
                extensionParameters: extensionParameters,
                bindingPropertyResolvers: bindingPropertyResolvers,
                serverSideOnly: serverSideOnly
            );
        }
    }
}
