using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics;

namespace DotVVM.Framework.Binding
{
    /// <summary> Represents a dictionary of values of <see cref="DotvvmPropertyGroup" />. </summary>
    public readonly struct VirtualPropertyGroupDictionary<TValue> : IDictionary<string, TValue>, IReadOnlyDictionary<string, TValue>
    {
        private readonly DotvvmBindableObject control;
        private readonly DotvvmPropertyGroup group;

        public VirtualPropertyGroupDictionary(DotvvmBindableObject control, DotvvmPropertyGroup group)
        {
            this.control = control;
            this.group = group;
        }

        DotvvmPropertyId GetMemberId(string key, bool createNew = false)
        {
            var memberId = DotvvmPropertyIdAssignment.GetGroupMemberId(key, registerIfNotFound: createNew);
            return DotvvmPropertyId.CreatePropertyGroupId(group.Id, memberId);
        }

        string GetMemberName(DotvvmPropertyId key)
        {
            return DotvvmPropertyIdAssignment.GetGroupMemberName((ushort)(key.Id & 0xFF_FF))!;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var (p, _) in control.properties.PropertyGroup(group.Id))
                {
                    yield return GetMemberName(p);
                }
            }
        }

        /// <summary> Lists all values. If any of the properties contains a binding, it will be automatically evaluated. </summary>
        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var (p, value) in control.properties.PropertyGroup(group.Id))
                {
                    yield return (TValue)control.EvalPropertyValue(group, value)!;
                }
            }
        }

        public IEnumerable<GroupedDotvvmProperty> Properties
        {
            get
            {
                foreach (var (p, _) in control.properties.PropertyGroup(group.Id))
                {
                    var prop = group.GetDotvvmProperty(p.MemberId);
                    yield return prop;
                }
            }
        }

        public int Count => control.properties.CountPropertyGroup(group.Id);

        public bool Any() => control.properties.ContainsPropertyGroup(group.Id);

        public bool IsReadOnly => false;

        ICollection<string> IDictionary<string, TValue>.Keys => Keys.ToList();

        ICollection<TValue> IDictionary<string, TValue>.Values => Values.ToList();

        /// <summary> Gets or sets value of property identified by <paramref name="key"/>. If the property contains a binding, the getter will automatically evaluate it. </summary>
        public TValue this[string key]
        {
            get
            {
                var p = GetMemberId(key);
                if (control.properties.TryGet(p, out var value))
                    return (TValue)control.EvalPropertyValue(group, value)!;
                else
                    return (TValue)group.DefaultValue!;
            }
            set
            {
                control.properties.Set(GetMemberId(key), value);
            }
        }

        /// <summary> Gets the value binding set to a specified property. Returns null if the property is not a binding, throws if the binding some kind of command. </summary>
        public IValueBinding? GetValueBinding(string key)
        {
            var binding = GetBinding(key);
            if (binding != null && binding is not IStaticValueBinding) // throw exception on incompatible binding types
            {
                throw new BindingHelper.BindingNotSupportedException(binding) { RelatedControl = control };
            }
            return binding as IValueBinding;

        }
        /// <summary> Gets the binding set to a specified property. Returns null if the property is not set or if the value is not a binding. </summary>
        public IBinding? GetBinding(string key) => GetValueRaw(key) as IBinding;
        /// <summary> Gets the value or a binding object for a specified property. </summary>
        public object? GetValueRaw(string key)
        {
            if (control.properties.TryGet(GetMemberId(key), out var value))
                return value;
            else
                return group.DefaultValue!;
        }

        /// <summary> Adds value or overwrites the property identified by <paramref name="key"/>. </summary>
        public void Set(string key, ValueOrBinding<TValue> value)
        {
            control.properties.Set(GetMemberId(key, createNew: true), value.UnwrapToObject());
        }
        /// <summary> Adds value or overwrites the property identified by <paramref name="key"/> with the value. </summary>
        public void Set(string key, TValue value) =>
            control.properties.Set(GetMemberId(key, createNew: true), value);
        /// <summary> Adds binding or overwrites the property identified by <paramref name="key"/> with the binding. </summary>
        public void SetBinding(string key, IBinding binding) =>
            control.properties.Set(GetMemberId(key, createNew: true), binding);

        public bool ContainsKey(string key)
        {
            return control.properties.Contains(GetMemberId(key));
        }

        private void AddOnConflict(DotvvmPropertyId id, string key, object? value)
        {
            var merger = this.group.ValueMerger;
            if (merger is null)
                throw new ArgumentException($"Cannot Add({key}, {value}) since the value is already set and merging is not enabled on this property group.");
            var mergedValue = merger.MergePlainValues(id, control.properties.GetOrThrow(id), value);
            control.properties.Set(id, mergedValue);
        }

        internal void AddInternal(ushort key, object? val)
        {
            var prop = DotvvmPropertyId.CreatePropertyGroupId(group.Id, key);
            if (!control.properties.TryAdd(prop, val))
                AddOnConflict(prop, prop.GroupMemberName.NotNull(), val);
        }
        /// <summary> Adds the property identified by <paramref name="key"/>. If the property is already set, it tries appending the value using the group's <see cref="Compilation.IAttributeValueMerger" /> </summary>
        public void Add(string key, ValueOrBinding<TValue> value)
        {
            var prop = GetMemberId(key, createNew: true);
            object? val = value.UnwrapToObject(); // TODO VOB boxing
            if (!control.properties.TryAdd(prop, val))
                AddOnConflict(prop, key, val);
        }

        /// <summary> Adds the property identified by <paramref name="key"/>. If the property is already set, it tries appending the value using the group's <see cref="Compilation.IAttributeValueMerger" /> </summary>
        public void Add(string key, TValue value) =>
            this.Add(key, new ValueOrBinding<TValue>(value));

        /// <summary> Adds the property identified by <paramref name="key"/>. If the property is already set, it tries appending the value using the group's <see cref="Compilation.IAttributeValueMerger" /> </summary>
        public void AddBinding(string key, IBinding? binding)
        {
            Add(key, new ValueOrBinding<TValue>(binding!));
        }

        public void CopyFrom(IEnumerable<KeyValuePair<string, TValue>> values, bool clear = false)
        {
            if (clear) this.Clear();
            foreach (var item in values)
            {
                Set(item.Key, item.Value);
            }
        }

        public void CopyFrom(IEnumerable<KeyValuePair<string, ValueOrBinding<TValue>>> values, bool clear = false)
        {
            if (clear) this.Clear();
            foreach (var item in values)
            {
                Set(item.Key, item.Value);
            }
        }
        public static IDictionary<string, TValue> CreateValueDictionary(DotvvmBindableObject control, DotvvmPropertyGroup group)
        {
            Dictionary<string, TValue> result;
#if NET8_0_OR_GREATER
            // don't bother counting without vector instructions
            if (Vector256.IsHardwareAccelerated)
            {
                var count = control.properties.CountPropertyGroup(group.Id);
                result = new(count);
                if (count == 0)
                    return result;
            }
            else
                result = new();
#else
            result = new();
#endif
            foreach (var (p, valueRaw) in control.properties)
            {
                if (p.IsInPropertyGroup(group.Id))
                {
                    var name = DotvvmPropertyIdAssignment.GetGroupMemberName((ushort)(p.Id & 0xFF_FF))!;
                    var valueObj = control.EvalPropertyValue(group, valueRaw);
                    if (valueObj is TValue value)
                        result.Add(name, value);
                    else if (valueObj is null)
                        result.Add(name, default!);
                }
            }
            return result;
        }

        public static IDictionary<string, ValueOrBinding<TValue>> CreatePropertyDictionary(DotvvmBindableObject control, DotvvmPropertyGroup group)
        {
            Dictionary<string, ValueOrBinding<TValue>> result;
#if NET8_0_OR_GREATER
            // don't bother counting without vector instructions
            if (Vector256.IsHardwareAccelerated)
            {
                var count = control.properties.CountPropertyGroup(group.Id);
                result = new(count);
                if (count == 0)
                    return result;
            }
            else
                result = new();
#else
            result = new();
#endif
            foreach (var (p, valRaw) in control.properties)
            {
                if (p.IsInPropertyGroup(group.Id))
                {
                    var name = DotvvmPropertyIdAssignment.GetGroupMemberName((ushort)(p.Id & 0xFF_FF))!;
                    result.Add(name, ValueOrBinding<TValue>.FromBoxedValue(valRaw));
                }
            }
            return result;
        }
        public bool Remove(string key)
        {
            return control.properties.Remove(GetMemberId(key));
        }

        /// <summary> Tries getting value of property identified by <paramref name="key"/>. If the property contains a binding, it will be automatically evaluated. </summary>
#pragma warning disable CS8767
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
#pragma warning restore CS8767
        {
            var memberId = DotvvmPropertyIdAssignment.GetGroupMemberId(key, registerIfNotFound: false);
            var p = DotvvmPropertyId.CreatePropertyGroupId(group.Id, memberId);
            if (control.properties.TryGet(p, out var valueRaw))
            {
                value = (TValue)control.EvalPropertyValue(group, valueRaw)!;
                return true;
            }
            else
            {
                value = default(TValue)!;
                return false;
            }
        }

        /// <summary> Adds the property-value pair to the dictionary. If the property is already set, it tries appending the value using the group's <see cref="Compilation.IAttributeValueMerger" /> </summary>
        public void Add(KeyValuePair<string, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            // we want to avoid allocating the list if there is only one property
            DotvvmPropertyId toRemove = default;
            List<DotvvmPropertyId>? toRemoveRest = null;

            foreach (var (p, _) in control.properties.PropertyGroup(group.Id))
            {
                if (toRemove.Id == 0)
                    toRemove = p;
                else
                {
                    toRemoveRest ??= new List<DotvvmPropertyId>();
                    toRemoveRest.Add(p);
                }
            }

            if (toRemove.Id != 0)
                control.properties.Remove(toRemove);

            if (toRemoveRest is {})
                foreach (var p in toRemoveRest)
                    control.properties.Remove(p);
        }

        public bool Contains(KeyValuePair<string, TValue> item)
        {
#pragma warning disable CS8717
            return TryGetValue(item.Key, out var realValue) && object.Equals(realValue, item.Value);
#pragma warning restore CS8717
        }

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
        {
            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public void CopyTo(IDictionary<string, TValue> dictionary)
        {
            foreach (var item in this)
            {
                dictionary[item.Key] = item.Value;
            }
        }

        public bool Remove(KeyValuePair<string, TValue> item)
        {
            if (Contains(item)) return Remove(item.Key);
            return false;
        }

        /// <summary> Enumerates all keys and values. If a property contains a binding, it will be automatically evaluated. </summary>
        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        {
            foreach (var (p, value) in control.properties.PropertyGroup(group.Id))
            {
                var name = GetMemberName(p);
                yield return new KeyValuePair<string, TValue>(name, (TValue)control.EvalPropertyValue(group, value)!);
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary> Enumerates all keys and values, without evaluating the bindings. </summary>
        public RawValuesCollection RawValues => new RawValuesCollection(this);

        public readonly struct RawValuesCollection: IEnumerable<KeyValuePair<string, object?>>, IReadOnlyDictionary<string, object?>
        {
            readonly VirtualPropertyGroupDictionary<TValue> self;

            internal RawValuesCollection(VirtualPropertyGroupDictionary<TValue> self)
            {
                this.self = self;
            }

            public object? this[string key] => self.GetValueRaw(key);
            public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value) =>
                self.control.properties.TryGet(self.GetMemberId(key), out value);

            public IEnumerable<string> Keys => self.Keys;

            public IEnumerable<object?> Values
            {
                get
                {
                    foreach (var (_, value) in self.control.properties.PropertyGroup(self.group.Id))
                        yield return value;
                }
            }

            public int Count => self.Count;

            public bool ContainsKey(string key) => self.ContainsKey(key);

            public RawValuesEnumerator GetEnumerator() => new RawValuesEnumerator(self.control.properties.EnumeratePropertyGroup(self.group.Id));
            IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public struct RawValuesEnumerator : IEnumerator<KeyValuePair<string, object?>>
        {
            private DotvvmControlPropertyIdGroupEnumerator inner;

            public KeyValuePair<string, object?> Current
            {
                get
                {
                    var (p, value) = inner.Current;
                    var mem = DotvvmPropertyIdAssignment.GetGroupMemberName((ushort)(p.Id & 0xFF_FF))!;
                    return new KeyValuePair<string, object?>(mem, value);
                }
            }

            object IEnumerator.Current => Current;

            public RawValuesEnumerator(DotvvmControlPropertyIdGroupEnumerator dotvvmControlPropertyIdEnumerator)
            {
                this.inner = dotvvmControlPropertyIdEnumerator;
            }

            public bool MoveNext() => inner.MoveNext();
            public void Reset() => inner.Reset();
            public void Dispose() => inner.Dispose();
        }
    }
}
