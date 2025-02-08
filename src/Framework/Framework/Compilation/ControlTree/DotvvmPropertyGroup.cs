using DotVVM.Framework.Binding;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Utils;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
using System.Threading;
using DotVVM.Framework.Binding.Expressions;

namespace DotVVM.Framework.Compilation.ControlTree
{
    /// <summary> A set of DotvvmProperties identified by a common prefix. For example RouteLink.Params-XX or html attributes are property groups. </summary>
    public class DotvvmPropertyGroup : IPropertyGroupDescriptor
    {
        public FieldInfo? DescriptorField { get; }

        public ICustomAttributeProvider AttributeProvider { get; }

        public string[] Prefixes { get; }

        public string Name { get; }

        public MarkupOptionsAttribute MarkupOptions { get; }

        public DataContextChangeAttribute[] DataContextChangeAttributes { get; }

        public DataContextStackManipulationAttribute? DataContextManipulationAttribute { get; }

        public object? DefaultValue { get; }

        public Type DeclaringType { get; }
        ITypeDescriptor IControlAttributeDescriptor.DeclaringType => new ResolvedTypeDescriptor(DeclaringType);

        public ObsoleteAttribute? ObsoleteAttribute { get; }
        public Type PropertyType { get; }
        ITypeDescriptor IControlAttributeDescriptor.PropertyType => new ResolvedTypeDescriptor(PropertyType);
        public IAttributeValueMerger? ValueMerger { get; }
        public bool IsBindingProperty { get; }
        internal ushort Id { get; }

        private readonly ConcurrentDictionary<ushort, WeakReference<GroupedDotvvmProperty>> generatedProperties = new();

        /// <summary> The capability which declared this property. When the property is declared by an capability, it can only be used by this capability. </summary>
        public DotvvmCapabilityProperty? OwningCapability { get; }
        IPropertyDescriptor? IControlAttributeDescriptor.OwningCapability => OwningCapability;
        /// <summary> The capabilities which use this property. </summary>
        public ImmutableArray<DotvvmCapabilityProperty> UsedInCapabilities { get; private set; } = ImmutableArray<DotvvmCapabilityProperty>.Empty;
        IEnumerable<IPropertyDescriptor> IControlAttributeDescriptor.UsedInCapabilities => UsedInCapabilities;

        internal DotvvmPropertyGroup(PrefixArray prefixes, Type valueType, Type declaringType, FieldInfo? descriptorField, ICustomAttributeProvider attributeProvider, string name, object? defaultValue, DotvvmCapabilityProperty? owningCapability = null)
        {
            this.DescriptorField = descriptorField;
            this.DeclaringType = declaringType;
            this.AttributeProvider = attributeProvider;
            this.Name = name;
            this.PropertyType = valueType;
            this.Prefixes = prefixes.Values;
            (this.MarkupOptions, this.DataContextChangeAttributes, this.DataContextManipulationAttribute, this.ObsoleteAttribute) = InitFromAttributes(attributeProvider, name);
            if (MarkupOptions.AllowValueMerging)
            {
                ValueMerger = (IAttributeValueMerger?)Activator.CreateInstance(MarkupOptions.AttributeValueMerger);
            }
            this.IsBindingProperty = typeof(IBinding).IsAssignableFrom(valueType);
            this.OwningCapability = owningCapability;
            this.Id = DotvvmPropertyIdAssignment.RegisterPropertyGroup(this);
        }

        private static (MarkupOptionsAttribute, DataContextChangeAttribute[], DataContextStackManipulationAttribute?, ObsoleteAttribute?)
            InitFromAttributes(ICustomAttributeProvider attributeProvider, string name)
        {
            var markupOptions = attributeProvider.GetCustomAttribute<MarkupOptionsAttribute>(true) ?? new MarkupOptionsAttribute();
            var dataContextChange = attributeProvider.GetCustomAttributes<DataContextChangeAttribute>(true);
            var dataContextManipulation = attributeProvider.GetCustomAttribute<DataContextStackManipulationAttribute>(true);
            if (dataContextManipulation != null && dataContextChange.Any()) throw new ArgumentException(
                $"{nameof(DataContextChangeAttributes)} and {nameof(DataContextManipulationAttribute)} cannot be set both at property group '{name}'.");
            var obsoleteAttribute = attributeProvider.GetCustomAttribute<ObsoleteAttribute>();
            return (markupOptions, dataContextChange.ToArray(), dataContextManipulation, obsoleteAttribute);
        }

        internal void AddUsedInCapability(DotvvmCapabilityProperty? p)
        {
            if (p is object)
                lock(this)
                {
                    if (UsedInCapabilities.Contains(p)) return;

                    var newArray = UsedInCapabilities.Add(p);
                    Thread.MemoryBarrier();
                    UsedInCapabilities = newArray;
                }
        }

        IPropertyDescriptor IPropertyGroupDescriptor.GetDotvvmProperty(string name) => GetDotvvmProperty(name);
        public GroupedDotvvmProperty GetDotvvmProperty(string name)
        {
            var id = DotvvmPropertyIdAssignment.GetGroupMemberId(name, registerIfNotFound: true);
            return GetDotvvmProperty(id);
        }

        public GroupedDotvvmProperty GetDotvvmProperty(ushort nameId)
        {
            while (true)
            {
                if (generatedProperties.TryGetValue(nameId, out var resultRef))
                {
                    if (resultRef.TryGetTarget(out var result))
                        return result;
                    else
                        generatedProperties.TryUpdate(nameId, new(CreateMemberProperty(nameId)), resultRef);
                }
                else
                {
                    generatedProperties.TryAdd(nameId, new(CreateMemberProperty(nameId)));
                }
            }
        }

        private GroupedDotvvmProperty CreateMemberProperty(ushort nameId)
        {
            var name = DotvvmPropertyIdAssignment.GetGroupMemberName(nameId).NotNull();
            return GroupedDotvvmProperty.Create(this, name, nameId);
        }

        private static ConcurrentDictionary<(Type, string), DotvvmPropertyGroup> descriptorDictionary = new();

        public static DotvvmPropertyGroup Register<TValue, TDeclaring>(PrefixArray prefixes, string name, TValue? defaultValue = default(TValue)) =>
            Register(typeof(TDeclaring), prefixes, name, typeof(TValue), defaultValue);

        public static DotvvmPropertyGroup Register(Type declaringType, PrefixArray prefixes, string name, Type valueType, object? defaultValue)
        {
            return descriptorDictionary.GetOrAdd((declaringType, name), fullName => {
                var field = declaringType.GetField(name + "GroupDescriptor", BindingFlags.Public | BindingFlags.Static);
                if (field == null) throw new InvalidOperationException($"Could not declare property group '{fullName}' because backing field was not found.");
                return new DotvvmPropertyGroup(prefixes, valueType, declaringType, field, field as ICustomAttributeProvider, name, defaultValue);
            });
        }
        internal static DotvvmPropertyGroup Register(DotvvmPropertyGroup group)
        {
            var key = (@group.DeclaringType, @group.Name);
            if (descriptorDictionary.ContainsKey(key))
            {
                throw new InvalidOperationException($"The property group {key} is already registered!");
            }
            descriptorDictionary[key] = group;
            return group;
        }
        internal static FieldInfo FindDescriptorField(Type declaringType, string name)
        {
            var fieldName = name + "GroupDescriptor";
            var field = declaringType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new InvalidOperationException($"Could not declare property group '{declaringType.Name}.{name}' because backing field {fieldName} was not found.");
            return field;
        }

        public static DotvvmPropertyGroup Register(Type declaringType, PrefixArray prefixes, string name, Type valueType, ICustomAttributeProvider attributeProvider, object? defaultValue, DotvvmCapabilityProperty? declaringCapability = null)
        {
            return descriptorDictionary.GetOrAdd((declaringType, name), fullName => {
                // the field is optional, here
                var field = declaringType.GetField(name + "GroupDescriptor", BindingFlags.Public | BindingFlags.Static);
                return new DotvvmPropertyGroup(prefixes, valueType, declaringType, field, attributeProvider, name, defaultValue, declaringCapability);
            });
        }

        public static IEnumerable<DotvvmPropertyGroup> FindAttachedPropertyCandidates(string typeName)
        {
            return descriptorDictionary.Values
                .Where(pg => pg.DeclaringType.Name == typeName);
        }

        public static IEnumerable<DotvvmPropertyGroup> AllGroups => descriptorDictionary.Values;

        public static DotvvmPropertyGroup? ResolvePropertyGroup(Type declaringType, string name)
        {
            return descriptorDictionary.TryGetValue((declaringType, name), out var group) ? group : null;
        }

        public static IPropertyDescriptor? ResolvePropertyGroup(string name, bool caseSensitive, MappingMode requiredMode = default)
        {
            var nameParts = name.Split('.');
            var groups = FindAttachedPropertyCandidates(nameParts[0])
                .SelectMany(g => g.Prefixes.Select(p => new { Group = g, Prefix = p }));

            var group = groups
                .FirstOrDefault(g =>
                    nameParts[1].StartsWith(g.Prefix, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) &&
                    g.Group.MarkupOptions.MappingMode.HasFlag(requiredMode));
            if (group != null)
            {
                var concreteName = nameParts[1].Substring(group.Prefix.Length);
                return group.Group.GetDotvvmProperty(concreteName);
            }

            return null;
        }

        public static IEnumerable<DotvvmPropertyGroup> GetPropertyGroups(Type controlType)
        {
            DefaultControlResolver.InitType(controlType);
            foreach (var pg in descriptorDictionary.Values)
            {
                if (pg.DeclaringType.IsAssignableFrom(controlType))
                {
                    yield return pg;
                }
            }
        }

        public static void CheckAllPropertiesAreRegistered(Type controlType)
        {
            var properties =
               (from p in controlType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                where !descriptorDictionary.ContainsKey((p.DeclaringType!, p.Name))
                where p.IsDefined(typeof(PropertyGroupAttribute))
                select p).ToArray();

            if (properties.Any())
            {
                var deprecationHelp = " DotVVM version <= 3.x did support this, but this feature was removed as it lead to many issues. Please register the property group using DotvvmPropertyGroup.Register and then use VirtualPropertyGroupDictionary<T> to access the values.";
                throw new NotSupportedException($"Control '{controlType.Name}' has property groups that are not registered: {string.Join(", ", properties.Select(p => p.Name))}." + deprecationHelp);
            }
        }


        public struct PrefixArray
        {
            public readonly string[] Values;

            public PrefixArray(string[] values)
            {
                this.Values = values;
            }

            public PrefixArray(string value)
            {
                this.Values = new[] { value };
            }

            public static implicit operator PrefixArray(string val) => new PrefixArray(val);
            public static implicit operator PrefixArray(string[] val) => new PrefixArray(val);
        }
    }
}
