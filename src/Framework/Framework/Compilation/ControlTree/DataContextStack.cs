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

            if (ExtensionParameters.Length >= 2)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in ExtensionParameters)
                {
                    if (!set.Add(p.Identifier))
                        throw new Exception($"Extension parameter '{p.Identifier}' is defined multiple times in the data context stack {this}");
                }
            }

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

        private string?[] ToStringFeatures() => [
            $"type={this.DataContextType.ToCode()}",
            this.ServerSideOnly ? "server-side-only" : null,
            this.NamespaceImports.Any() ? "imports=[" + string.Join(", ", this.NamespaceImports) + "]" : null,
            this.ExtensionParameters.Any() ? "ext=[" + string.Join(", ", this.ExtensionParameters.Select(e => e.Identifier + ": " + e.ParameterType.CSharpName)) + "]" : null,
            this.BindingPropertyResolvers.Any() ? "resolvers=[" + string.Join(", ", this.BindingPropertyResolvers.Select(s => s.Method)) + "]" : null,
            this.Parent != null ? "par=[" + string.Join(", ", this.Parents().Select(p => p.ToCode(stripNamespace: true))) + "]" : null
        ];

        public override string ToString() =>
            "(" + ToStringFeatures().WhereNotNull().StringJoin(", ") + ")";

        private string ToStringWithoutParent() =>
            ToStringFeatures()[..^1].WhereNotNull().StringJoin(", ");


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
            extensionParameters = [..(extensionParameters ?? []), ..indexParameters ];
            return DataContextStack.Create(
                elementType, parent,
                imports: imports,
                extensionParameters: extensionParameters,
                bindingPropertyResolvers: bindingPropertyResolvers,
                serverSideOnly: serverSideOnly
            );
        }

        private static int Difference(DataContextStack a, DataContextStack b)
        {
            if (a == b) return 0;

            var result = 0;
            if (a.DataContextType != b.DataContextType)
                result += 6;
            
            if (a.DataContextType.Namespace != b.DataContextType.Namespace)
                result += 2;
            
            if (a.DataContextType.Name != b.DataContextType.Name)
                result += 2;

            result += CompareSets(a.NamespaceImports, b.NamespaceImports);

            result += CompareSets(a.ExtensionParameters, b.ExtensionParameters);

            result += CompareSets(a.BindingPropertyResolvers, b.BindingPropertyResolvers);

            if (a.Parent != b.Parent)
                result += 1;

            return result;


            static int CompareSets<T>(IEnumerable<T> a, IEnumerable<T> b)
            {
                return a.Union(b).Count() - a.Intersect(b).Count();
            }
        }

        public static (string a, string b)[][] CompareStacksMessage(DataContextStack a, DataContextStack b)
        {
            var alignment = StringSimilarity.SequenceAlignment<DataContextStack>(
                a.EnumerableItems().ToArray().AsSpan(), b.EnumerableItems().ToArray().AsSpan(),
                Difference,
                gapCost: 10);

            return alignment.Select(pair => {
                return CompareMessage(pair.a, pair.b);
            }).ToArray();
        }

        /// <summary> Provides a formatted string for two DataContextStacks with aligned fragments used for highlighting. Does not include the parent context. </summary>
        public static (string a, string b)[] CompareMessage(DataContextStack? a, DataContextStack? b)
        {
            if (a == null || b == null) return new[] { (a?.ToStringWithoutParent() ?? "(missing)", b?.ToStringWithoutParent() ?? "(missing)") };

            var result = new List<(string, string)>();

            void same(string str) => result.Add((str, str));
            void different(string? a, string? b) => result.Add((a ?? "", b ?? ""));
            
            same("type=");
            if (a.DataContextType == b.DataContextType)
                same(a.DataContextType.ToCode(stripNamespace: true));
            else
            {
                different(a.DataContextType.Namespace, b.DataContextType.Namespace);
                same(".");
                different(a.DataContextType.ToCode(stripNamespace: true), b.DataContextType.ToCode(stripNamespace: true));
            }

            if (a.ServerSideOnly || b.ServerSideOnly)
            {
                same(", ");
                different(a.ServerSideOnly ? "server-side-only" : "", b.ServerSideOnly ? "server-side-only" : "");
            }

            if (a.NamespaceImports.Any() || b.NamespaceImports.Any())
            {
                same(", imports=[");
                var importsAligned = StringSimilarity.SequenceAlignment(
                    a.NamespaceImports.AsSpan(), b.NamespaceImports.AsSpan(),
                    (a, b) => a.Equals(b) ? 0 :
                              a.Namespace == b.Namespace || a.Alias == b.Alias ? 1 :
                              3,
                    gapCost: 2);
                foreach (var (i, (aImport, bImport)) in importsAligned.Indexed())
                {
                    if (i > 0)
                        same(", ");

                    different(aImport.ToString(), bImport.ToString());
                }

                same("]");
            }

            if (a.ExtensionParameters.Any() || b.ExtensionParameters.Any())
            {
                same(", ext=[");
                var extAligned = StringSimilarity.SequenceAlignment(
                    a.ExtensionParameters.AsSpan(), b.ExtensionParameters.AsSpan(),
                    (a, b) => a.Equals(b) ? 0 :
                              a.Identifier == b.Identifier ? 1 :
                              3,
                    gapCost: 2);
                foreach (var (i, (aExt, bExt)) in extAligned.Indexed())
                {
                    if (i > 0)
                        same(", ");

                    if (Equals(aExt, bExt))
                        same(aExt!.Identifier);
                    else if (aExt is null)
                        different("", bExt!.Identifier + ": " + bExt.ParameterType.CSharpName);
                    else if (bExt is null)
                        different(aExt.Identifier + ": " + aExt.ParameterType.CSharpName, "");
                    else
                    {
                        different(aExt.Identifier, bExt.Identifier);
                        same(": ");
                        if (aExt.ParameterType.IsEqualTo(bExt.ParameterType))
                            same(aExt.ParameterType.CSharpName);
                        else
                            different(aExt.ParameterType.CSharpFullName, bExt.ParameterType.CSharpFullName);

                        if (aExt.Identifier == bExt.Identifier && aExt.GetType() != bExt.GetType())
                        {
                            same(" (");
                            different(aExt.GetType().ToCode(), bExt.GetType().ToCode());
                            same(")");
                        }
                    }
                }

                same("]");
            }

            return result.ToArray();
        }
    }
}
