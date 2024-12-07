using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct DotvvmControlProperties : IEnumerable<KeyValuePair<DotvvmPropertyId, object?>>
    {
        // There are 3 possible states of this structure:
        // 1. keys == values == null --> it is empty
        // 2. keys == null & values is Dictionary<DotvvmPropertyId, object> --> it falls back to traditional mutable property dictionary
        // 3. keys is DotvvmPropertyId[] & values is object[] --> read-only perfect 2-slot hashing
        [FieldOffset(0)]
        private DotvvmPropertyId[]? keys;

        [FieldOffset(8)]
        private object? values;

        [FieldOffset(8)]
        private object?[] valuesAsArray;

        [FieldOffset(8)]
        private Dictionary<DotvvmPropertyId, object?> valuesAsDictionary;

        /// <summary>
        /// flags >> 31: 1bit - ownsValues
        /// flags >> 30: 1bit - ownsKeys
        /// flags >> 0: 30bits - hashSeed
        /// </summary>
        [FieldOffset(16)]
        private uint flags;
        private uint hashSeed
        {
            readonly get => flags & 0x3F_FF_FF_FF;
            set => flags = (flags & ~0x3F_FF_FF_FFu) | value;
        }
        private bool ownsKeys
        {
            readonly get => (flags >> 30) != 0;
            set => flags = (flags & ~(1u << 30)) | ((uint)BoolToInt(value) << 30);
        }
        private bool ownsValues
        {
            readonly get => (flags >> 31) != 0;
            set => flags = (flags & ~(1u << 31)) | ((uint)BoolToInt(value) << 31);
        }

        public void AssignBulk(DotvvmPropertyId[] keys, object?[] values, uint flags)
        {
            // The explicit layout is quite likely to mess up with array covariance, just make sure we don't encounter that
            Debug.Assert(values.GetType() == typeof(object[]));
            Debug.Assert(keys.GetType() == typeof(DotvvmPropertyId[]));
            Debug.Assert(keys.Length == values.Length);
            if (this.values == null || Object.ReferenceEquals(this.keys, keys))
            {
                this.valuesAsArray = values;
                this.keys = keys;
                this.flags = flags;
            }
            else
            {
                // we can just to check if all current properties are in the proposed set
                // if they are not we will have to copy it

                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].Id != 0)
                        this.Set(keys[i]!, values[i]);
                }
            }
        }

        public void AssignBulk(Dictionary<DotvvmPropertyId, object?> values, bool owns)
        {
            if (this.values == null || object.ReferenceEquals(this.values, values))
            {
                this.keys = null;
                this.valuesAsDictionary = values;
                this.flags = (uint)BoolToInt(owns) << 31;
            }
            else
            {
                if (owns)
                {
                    foreach (var (k, v) in this)
                    {
                        values.TryAdd(k, v);
                    }
                    this.values = values;
                    this.keys = null;
                    this.flags = 1u << 31;
                }
                else
                {
                    foreach (var (k, v) in values)
                    {
                        this.Set(k, v);
                    }
                }
            }
        }

        public void ClearEverything()
        {
            values = null;
            keys = null;
        }

        public readonly bool Contains(DotvvmProperty p) => Contains(p.Id);
        public readonly bool Contains(DotvvmPropertyId p)
        {
            if (keys is {})
            {
                Debug.Assert(values is object[]);
                Debug.Assert(keys is DotvvmPropertyId[]);
                return PropertyImmutableHashtable.ContainsKey(this.keys, this.hashSeed, p);
            }
            else if (values is null) { return false; }
            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
                return valuesAsDictionary.ContainsKey(p);
            }
        }

        public readonly bool ContainsPropertyGroup(DotvvmPropertyGroup group) => ContainsPropertyGroup(group.Id);
        public readonly bool ContainsPropertyGroup(ushort groupId)
        {

            if (keys is {})
            {
                return PropertyImmutableHashtable.ContainsPropertyGroup(this.keys, groupId);
            }
            else if (values is null) return false;
            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
                foreach (var key in valuesAsDictionary.Keys)
                {
                    if (key.IsInPropertyGroup(groupId))
                        return true;
                }
                return false;
            }
        }

        public readonly int CountPropertyGroup(DotvvmPropertyGroup group) => CountPropertyGroup(group.Id);
        public readonly int CountPropertyGroup(ushort groupId)
        {
            if (keys is {})
            {
                return PropertyImmutableHashtable.CountPropertyGroup(this.keys, groupId);
            }
            else if (values is null) return 0;
            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
                int count = 0;
                foreach (var key in valuesAsDictionary.Keys)
                {
                    if (key.IsInPropertyGroup(groupId))
                        count++;
                }
                return count;
            }
        }

        public readonly bool TryGet(DotvvmProperty p, out object? value) => TryGet(p.Id, out value);
        public readonly bool TryGet(DotvvmPropertyId p, out object? value)
        {
            if (keys != null)
            {
                Debug.Assert(values is object[]);
                Debug.Assert(keys is not null);
                var index = PropertyImmutableHashtable.FindSlot(this.keys, this.hashSeed, p);
                if (index >= 0)
                {
                    value = this.valuesAsArray[index];
                    return true;
                }
                else
                {
                    value = null;
                    return false;
                }
            }

            else if (values == null) { value = null; return false; }

            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
                return valuesAsDictionary.TryGetValue(p, out value);
            }
        }

        public readonly object? GetOrThrow(DotvvmProperty p) => GetOrThrow(p.Id);
        public readonly object? GetOrThrow(DotvvmPropertyId p)
        {
            if (this.TryGet(p, out var x)) return x;
            throw new KeyNotFoundException();
        }

        public void Set(DotvvmProperty p, object? value) => Set(p.Id, value);
        public void Set(DotvvmPropertyId p, object? value)
        {
            if (p.MemberId == 0)
                throw new ArgumentException("Invalid (unitialized) property id cannot be set into the DotvvmControlProperties dictionary.", nameof(p));

            if (keys != null)
            {
                Debug.Assert(values is object[]);
                Debug.Assert(keys is DotvvmPropertyId[]);
                var slot = PropertyImmutableHashtable.FindSlotOrFree(keys, hashSeed, p, out var exists);
                if (slot >= 0)
                {
                    if (!exists)
                    {
                        OwnKeys();
                        OwnValues();
                        keys[slot] = p;
                        valuesAsArray[slot] = value;
                    }
                    else if (Object.ReferenceEquals(valuesAsArray[slot], value))
                    {
                        // no-op, we would be changing it to the same value
                    }
                    else
                    {
                        this.OwnValues();
                        valuesAsArray[slot] = value;
                    }
                }
                else
                {
                    SwitchToDictionary();
                    Debug.Assert(values is Dictionary<DotvvmPropertyId, object?>);
                    valuesAsDictionary[p] = value;
                    keys = null;
                }
            }
            else if (values == null)
            {
                Debug.Assert(keys == null);
                this.flags = 0b11u << 30;
                this.keys = new DotvvmPropertyId[PropertyImmutableHashtable.AdhocTableSize];
                this.keys[0] = p;
                this.valuesAsArray = new object?[PropertyImmutableHashtable.AdhocTableSize];
                this.valuesAsArray[0] = value;
            }
            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object?>);
                valuesAsDictionary[p] = value;
            }
        }

        /// <summary> Tries to set value into the dictionary without overwriting anything. </summary>
        public bool TryAdd(DotvvmProperty p, object? value) => TryAdd(p.Id, value);

        /// <summary> Tries to set value into the dictionary without overwriting anything. </summary>
        public bool TryAdd(DotvvmPropertyId p, object? value)
        {
            if (keys != null)
            {
                Debug.Assert(this.values is object[]);
                Debug.Assert(keys is DotvvmPropertyId[]);
                var slot = PropertyImmutableHashtable.FindSlotOrFree(keys, this.hashSeed, p, out var exists);
                if (slot >= 0)
                {
                    if (exists)
                    {
                        // value already exists
                        return Object.ReferenceEquals(valuesAsArray[slot], value);
                    }
                    else
                    {
                        OwnKeys();
                        OwnValues();
                        // set the value
                        keys[slot] = p;
                        valuesAsArray[slot] = value;
                        return true;
                    }
                }
                else
                {
                    // no free slots, move to standard Dictionary
                    SwitchToDictionary();
                    this.valuesAsDictionary.Add(p, value);
                    return true;
                }
            }
            if (values == null)
            {
                // empty dict -> initialize 8-slot array
                Debug.Assert(keys == null);
                this.flags = 0b11u << 30;
                this.keys = new DotvvmPropertyId[PropertyImmutableHashtable.AdhocTableSize];
                this.keys[0] = p;
                this.valuesAsArray = new object?[PropertyImmutableHashtable.AdhocTableSize];
                this.valuesAsArray[0] = value;
                return true;
            }
            else
            {
                // System.Dictionary backend
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object?>);
#if CSharp8Polyfill
                if (valuesAsDictionary.TryGetValue(p, out var existingValue))
                    return Object.ReferenceEquals(existingValue, value);
                else
                {
                    valuesAsDictionary.Add(p, value);
                    return true;
                }
#else
                return valuesAsDictionary.TryAdd(p, value) || Object.ReferenceEquals(valuesAsDictionary[p], value);
#endif
            }
        }


        public readonly DotvvmControlPropertyIdEnumerator GetEnumerator()
        {
            if (keys != null) return new DotvvmControlPropertyIdEnumerator(keys, valuesAsArray);

            if (values == null) return EmptyEnumerator;

            Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
            return new DotvvmControlPropertyIdEnumerator(valuesAsDictionary.GetEnumerator());
        }

        readonly IEnumerator<KeyValuePair<DotvvmPropertyId, object?>> IEnumerable<KeyValuePair<DotvvmPropertyId, object?>>.GetEnumerator() =>
            GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public readonly PropertyGroupEnumerable PropertyGroup(ushort groupId) => new(in this, groupId);

        readonly public DotvvmControlPropertyIdGroupEnumerator EnumeratePropertyGroup(ushort id) =>
            this.keys is {} keys ? new(keys, valuesAsArray, id) :
            this.values is {} ? new(valuesAsDictionary.GetEnumerator(), id) :
            default;

        private static readonly DotvvmControlPropertyIdEnumerator EmptyEnumerator = new DotvvmControlPropertyIdEnumerator(Array.Empty<DotvvmPropertyId>(), Array.Empty<object>());

        public bool Remove(DotvvmProperty key) => Remove(key.Id);
        public bool Remove(DotvvmPropertyId key)
        {
            if (this.keys != null)
            {
                var slot = PropertyImmutableHashtable.FindSlot(this.keys, this.hashSeed, key);
                if (slot < 0)
                    return false;
                this.OwnKeys();
                this.keys[slot] = default;
                if (this.ownsValues)
                {
                    this.valuesAsArray[slot] = default;
                }
                return true;
            }
            if (this.values == null)
                return false;
            else
            {
                Debug.Assert(values is Dictionary<DotvvmPropertyId, object>);
                return valuesAsDictionary.Remove(key);
            }
        }

        private static object? CloneValue(object? value)
        {
            if (value is DotvvmBindableObject bindableObject)
                return bindableObject.CloneControl();
            return null;
        }

        public readonly int Count()
        {
            if (this.values == null) return 0;
            if (this.keys == null)
                return this.valuesAsDictionary.Count;
            
            return PropertyImmutableHashtable.Count(this.keys);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte BoolToInt(bool x) => Unsafe.As<bool, byte>(ref x);

        internal void CloneInto(ref DotvvmControlProperties newDict)
        {
            if (this.values == null)
            {
                newDict = default;
                return;
            }
            else if (this.keys == null)
            {
                var dictionary = this.valuesAsDictionary;
                if (dictionary.Count > 30)
                {
                    newDict = this;
                    newDict.keys = null;
                    newDict.valuesAsDictionary = new Dictionary<DotvvmPropertyId, object?>(dictionary);
                    foreach (var (key, value) in dictionary)
                        if (CloneValue(value) is {} newValue)
                            newDict.valuesAsDictionary[key] = newValue;
                    return;
                }
                // move to immutable version if it's reasonably small. It will be probably cloned multiple times again
                SwitchToPerfectHashing();
            }

            newDict = this;
            newDict.ownsKeys = false;
            newDict.ownsValues = false;
            for (int i = 0; i < newDict.valuesAsArray.Length; i++)
            {
                if (CloneValue(newDict.valuesAsArray[i]) is {} newValue)
                {
                    // clone the array if we didn't do that already
                    if (newDict.values == this.values)
                    {
                        newDict.values = this.valuesAsArray.Clone();
                        newDict.ownsValues = true;
                    }

                    newDict.valuesAsArray[i] = newValue;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OwnKeys()
        {
            if (this.ownsKeys) return;
            CloneKeys();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OwnValues()
        {
            if (this.ownsValues) return;
            CloneValues();
        }
        void CloneKeys()
        {
            var oldKeys = this.keys;
            var newKeys = new DotvvmPropertyId[oldKeys!.Length];
            MemoryExtensions.CopyTo(oldKeys, newKeys.AsSpan());
            this.keys = newKeys;
            this.ownsKeys = true;
        }
        void CloneValues()
        {
            if (keys is {})
            {
                var oldValues = this.valuesAsArray;
                var newValues = new object?[oldValues.Length];
                MemoryExtensions.CopyTo(oldValues, newValues.AsSpan());
                this.valuesAsArray = newValues;
                this.ownsValues = true;
            }
            else if (values is null)
                return;
            else
            {
                this.valuesAsDictionary = new Dictionary<DotvvmPropertyId, object?>(this.valuesAsDictionary);
                this.flags = 1u << 31;
            }
        }

        /// <summary> Converts the internal representation to System.Collections.Generic.Dictionary </summary>
        void SwitchToDictionary()
        {
            if (this.keys is {})
            {
                var keysTmp = this.keys;
                var valuesTmp = this.valuesAsArray;
                var d = new Dictionary<DotvvmPropertyId, object?>(capacity: keysTmp.Length);

                for (int i = 0; i < keysTmp.Length; i++)
                {
                    if (keysTmp[i].Id != 0)
                        d[keysTmp[i]] = valuesTmp[i];
                }
                this.valuesAsDictionary = d;
                this.keys = null;
                this.flags = 1u << 31;
            }
            else if (this.values is null)
            {
                // already in the dictionary
                return;
            }
            else
            {
                Debug.Assert(this.values is null);
                // empty state
                this.valuesAsDictionary = new Dictionary<DotvvmPropertyId, object?>();
                this.flags = 1u << 31;
            }
        }

        /// <summary> Converts the internal representation to the DotVVM small dictionary implementation </summary>
        void SwitchToPerfectHashing()
        {
            if (this.keys is {})
            {
                // already in the perfect hashing
                return;
            }
            else if (this.values is {})
            {
                var properties = new DotvvmPropertyId[valuesAsDictionary.Count];
                var values = new object?[properties.Length];
                int j = 0;
                foreach (var x in this.valuesAsDictionary)
                {
                    (properties[j], values[j]) = x;
                    j++;
                }
                Array.Sort(properties, values);
                (this.hashSeed, this.keys, this.valuesAsArray) = PropertyImmutableHashtable.CreateTableWithValues(properties, values);
                this.ownsKeys = false;
                this.ownsValues = true;
            }
            else
            {
            }
        }

        public readonly struct PropertyGroupEnumerable: IEnumerable<KeyValuePair<DotvvmPropertyId, object?>>
        {
            private readonly DotvvmControlProperties properties;
            private readonly ushort groupId;
            public PropertyGroupEnumerable(in DotvvmControlProperties properties, ushort groupId)
            {
                this.properties = properties;
                this.groupId = groupId;
            }

            public IEnumerator<KeyValuePair<DotvvmPropertyId, object?>> GetEnumerator() => properties.EnumeratePropertyGroup(groupId);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    public struct DotvvmControlPropertyIdGroupEnumerator : IEnumerator<KeyValuePair<DotvvmPropertyId, object?>>
    {
        private DotvvmPropertyId[]? keys;
        private object?[]? values;
        private int index;
        private ushort groupId;
        private ushort bitmap; // TODO!!
        private Dictionary<DotvvmPropertyId, object?>.Enumerator dictEnumerator;

        internal DotvvmControlPropertyIdGroupEnumerator(DotvvmPropertyId[] keys, object?[] values, ushort groupId)
        {
            this.keys = keys;
            this.values = values;
            this.index = -1;
            this.groupId = groupId;
            this.bitmap = 0;
            dictEnumerator = default;
        }

        internal DotvvmControlPropertyIdGroupEnumerator(Dictionary<DotvvmPropertyId, object?>.Enumerator e, ushort groupId)
        {
            this.keys = null;
            this.values = null;
            this.index = 0;
            this.groupId = groupId;
            this.dictEnumerator = e;
        }

        public KeyValuePair<DotvvmPropertyId, object?> Current => this.keys is {} keys ? new(keys[index]!, values![index]) : dictEnumerator.Current;

        object IEnumerator.Current => this.Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            var keys = this.keys;
            if (keys is {})
            {
                var index = (uint)(this.index + 1);
                var bitmap = this.bitmap;
                while (index < keys.Length)
                {
                    if (index % 16 == 0)
                    {
                        bitmap = PropertyImmutableHashtable.FindGroupInNext16Slots(keys, index, groupId);
                    }
                    var localIndex = BitOperations.TrailingZeroCount(bitmap);
                    if (localIndex < 16)
                    {
                        this.index = (int)index + localIndex;
                        this.bitmap = (ushort)(bitmap >> (localIndex + 1));
                        return true;
                    }
                    index += 16;
                }
                this.index = keys.Length;
                return false;
            }
            else
            {
                // `default(T)` - empty collection
                if (groupId == 0)
                    return false;

                while (dictEnumerator.MoveNext())
                {
                    if (dictEnumerator.Current.Key.IsInPropertyGroup(groupId))
                        return true;
                }
                return false;
            }
        }

        public void Reset()
        {
            if (keys == null)
                ((IEnumerator)dictEnumerator).Reset();
            else index = -1;
        }
    }

    public struct DotvvmControlPropertyIdEnumerator : IEnumerator<KeyValuePair<DotvvmPropertyId, object?>>
    {
        private DotvvmPropertyId[]? keys;
        private object?[]? values;
        private int index;
        private Dictionary<DotvvmPropertyId, object?>.Enumerator dictEnumerator;

        internal DotvvmControlPropertyIdEnumerator(DotvvmPropertyId[] keys, object?[] values)
        {
            this.keys = keys;
            this.values = values;
            this.index = -1;
            dictEnumerator = default;
        }

        internal DotvvmControlPropertyIdEnumerator(Dictionary<DotvvmPropertyId, object?>.Enumerator e)
        {
            this.keys = null;
            this.values = null;
            this.index = 0;
            this.dictEnumerator = e;
        }

        public KeyValuePair<DotvvmPropertyId, object?> Current => this.keys is {} keys ? new(keys[index]!, values![index]) : dictEnumerator.Current;

        object IEnumerator.Current => this.Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            var keys = this.keys;
            if (keys is null)
                return dictEnumerator.MoveNext();
            var index = this.index;
            while (++index < keys.Length && keys[index].Id == 0) { }
            this.index = index;
            return index < keys.Length;
        }

        public void Reset()
        {
            if (keys is null)
                ((IEnumerator)dictEnumerator).Reset();
            else index = -1;
        }
    }

    public struct DotvvmControlPropertiesEnumerator : IEnumerator<KeyValuePair<DotvvmProperty, object?>>
    {
        DotvvmControlPropertyIdEnumerator idEnumerator;
        public DotvvmControlPropertiesEnumerator(DotvvmControlPropertyIdEnumerator idEnumerator)
        {
            this.idEnumerator = idEnumerator;
        }

        public KeyValuePair<DotvvmProperty, object?> Current
        {
            get
            {
                var x = idEnumerator.Current;
                return new KeyValuePair<DotvvmProperty, object?>(x.Key.PropertyInstance, x.Value);
            }
        }

        object IEnumerator.Current => this.Current;

        public void Dispose() => idEnumerator.Dispose();
        public bool MoveNext() => idEnumerator.MoveNext();
        public void Reset() => idEnumerator.Reset();
    }

    public readonly struct DotvvmPropertyDictionary : IDictionary<DotvvmProperty, object?>
    {

        private readonly DotvvmBindableObject control;

        public DotvvmPropertyDictionary(DotvvmBindableObject control)
        {
            this.control = control;
        }

        public object? this[DotvvmProperty key] { get => control.properties.GetOrThrow(key); set => control.properties.Set(key, value); }

        public ICollection<DotvvmProperty> Keys => throw new NotImplementedException();

        public ICollection<object?> Values => throw new NotImplementedException();

        public int Count => control.properties.Count();
        public bool IsReadOnly => false;

        public void Add(DotvvmProperty key, object? value)
        {
            if (!control.properties.TryAdd(key, value))
                throw new System.ArgumentException("An item with the same key has already been added.");
        }
        public bool TryAdd(DotvvmProperty key, object? value)
        {
            return control.properties.TryAdd(key, value);
        }

        public void Add(KeyValuePair<DotvvmProperty, object?> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            control.properties.ClearEverything();
        }

        public bool Contains(KeyValuePair<DotvvmProperty, object?> item) => control.properties.TryGet(item.Key, out var x) && Object.Equals(x, item.Value);

        public bool ContainsKey(DotvvmProperty key) => control.properties.Contains(key);

        public void CopyTo(KeyValuePair<DotvvmProperty, object?>[] array, int arrayIndex)
        {
            foreach (var x in this)
            {
                array[arrayIndex++] = x;
            }
        }

        public DotvvmControlPropertiesEnumerator GetEnumerator() => new(control.properties.GetEnumerator());

        public bool Remove(DotvvmProperty key)
        {
            return control.properties.Remove(key);
        }

        public bool Remove(KeyValuePair<DotvvmProperty, object?> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(DotvvmProperty key, out object? value) =>
            control.properties.TryGet(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        IEnumerator<KeyValuePair<DotvvmProperty, object?>> IEnumerable<KeyValuePair<DotvvmProperty, object?>>.GetEnumerator() => this.GetEnumerator();
    }
}
