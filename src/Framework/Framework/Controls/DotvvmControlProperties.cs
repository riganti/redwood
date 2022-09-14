using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct DotvvmControlProperties : IEnumerable<KeyValuePair<DotvvmProperty, object?>>
    {
        // There are 3 possible states of this structure:
        // 1. keys == values == null --> it is empty
        // 2. keys == null & values is Dictionary<DotvvmProperty, object> --> it falls back to traditional mutable property dictionary
        // 3. keys is DotvvmProperty[] & values is object[] --> read-only perfect 2-slot hashing
        [FieldOffset(0)]
        private object? keys;

        [FieldOffset(0)]
        private DotvvmProperty?[] keysAsArray;

        [FieldOffset(8)]
        private object? values;

        [FieldOffset(8)]
        private object?[] valuesAsArray;

        [FieldOffset(8)]
        private Dictionary<DotvvmProperty, object?> valuesAsDictionary;

        [FieldOffset(16)]
        private int hashSeed;

        public void AssignBulk(DotvvmProperty?[] keys, object?[] values, int hashSeed)
        {
            // The explicit layout is quite likely to mess with array covariance, just make sure we don't encounter that
            Debug.Assert(values.GetType() == typeof(object[]));
            Debug.Assert(keys.GetType() == typeof(DotvvmProperty[]));
            Debug.Assert(keys.Length == values.Length);
            if (this.values == null || this.keys == keys)
            {
                this.valuesAsArray = values;
                this.keysAsArray = keys;
                this.hashSeed = hashSeed;
            }
            else
            {
                // we can just to check if all current properties are in the proposed set
                // if they are not we will have to copy it

                if (this.keys == null) // TODO: is this heuristic actually useful?
                {
                    var ok = true;
                    foreach (var x in (Dictionary<DotvvmProperty, object>)this.values)
                    {
                        var e = PropertyImmutableHashtable.FindSlot(keys, hashSeed, x.Key);
                        if (e < 0 || !Object.Equals(values[e], x.Value))
                            ok = false;
                    }
                    if (ok)
                    {
                        this.values = values;
                        this.keys = keys;
                        this.hashSeed = hashSeed;
                        return;
                    }
                }

                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] != null)
                        this.Set(keys[i]!, values[i]);
                }
            }
        }

        public void ClearEverything()
        {
            values = null;
            keys = null;
        }

        public bool Contains(DotvvmProperty p)
        {
            if (values == null) { return false; }

            if (keys == null)
            {
                Debug.Assert(values is Dictionary<DotvvmProperty, object>);
                return valuesAsDictionary.ContainsKey(p);
            }

            Debug.Assert(values is object[]);
            Debug.Assert(keys is DotvvmProperty[]);
            return PropertyImmutableHashtable.ContainsKey(this.keysAsArray, this.hashSeed, p);
        }

        public bool TryGet(DotvvmProperty p, out object? value)
        {
            value = null;
            if (values == null) { return false; }

            if (keys == null)
            {
                Debug.Assert(values is Dictionary<DotvvmProperty, object>);
                return valuesAsDictionary.TryGetValue(p, out value);
            }

            Debug.Assert(values is object[]);
            Debug.Assert(keys is DotvvmProperty[]);
            var index = PropertyImmutableHashtable.FindSlot(this.keysAsArray, this.hashSeed, p);
            if (index != -1)
                value = this.valuesAsArray[index & (this.keysAsArray.Length - 1)];
            return index != -1;
        }

        public object? GetOrThrow(DotvvmProperty p)
        {
            if (this.TryGet(p, out var x)) return x;
            throw new KeyNotFoundException();
        }

        public void Set(DotvvmProperty p, object? value)
        {
            if (values == null)
            {
                Debug.Assert(keys == null);
                var d = new Dictionary<DotvvmProperty, object?>();
                d[p] = value;
                this.values = d;
            }
            else if (keys == null)
            {
                Debug.Assert(values is Dictionary<DotvvmProperty, object?>);
                valuesAsDictionary[p] = value;
            }
            else
            {
                Debug.Assert(this.values is object[]);
                Debug.Assert(this.keys is DotvvmProperty[]);
                var keys = this.keysAsArray;
                var values = this.valuesAsArray;
                var slot = PropertyImmutableHashtable.FindSlot(keys, this.hashSeed, p);
                if (slot >= 0 && Object.Equals(values[slot], value))
                {
                    // no-op, we would be changing it to the same value
                }
                else
                {
                    var d = new Dictionary<DotvvmProperty, object?>();
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i] != null)
                            d[keys[i]!] = values[i];
                    }
                    d[p] = value;
                    this.valuesAsDictionary = d;
                    this.keys = null;
                }
            }
        }

        /// <summary> Tries to set value into the dictionary without overwriting anything. </summary>
        public bool TryAdd(DotvvmProperty p, object? value)
        {
            if (values == null)
            {
                Debug.Assert(keys == null);
                var d = new Dictionary<DotvvmProperty, object?>();
                d[p] = value;
                this.values = d;
                return true;
            }
            else if (keys == null)
            {
                Debug.Assert(values is Dictionary<DotvvmProperty, object?>);
#if CSharp8Polyfill
                if (valuesAsDictionary.TryGetValue(p, out var existingValue))
                    return Object.Equals(existingValue, value);
                else
                {
                    valuesAsDictionary.Add(p, value);
                    return true;
                }
#else
                if (valuesAsDictionary.TryAdd(p, value))
                    return true;
                else
                    return Object.Equals(valuesAsDictionary[p], value);
#endif
            }
            else
            {
                Debug.Assert(this.values is object[]);
                Debug.Assert(this.keys is DotvvmProperty[]);
                var keys = this.keysAsArray;
                var values = this.valuesAsArray;
                var slot = PropertyImmutableHashtable.FindSlot(keys, this.hashSeed, p);
                if (slot >= 0)
                {
                    // value already exists
                    return Object.Equals(values[slot], value);
                }
                else
                {
                    var d = new Dictionary<DotvvmProperty, object?>();
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i] != null)
                            d[keys[i]!] = values[i];
                    }
                    d[p] = value;
                    this.valuesAsDictionary = d;
                    this.keys = null;
                    return true;
                }
            }
        }


        public DotvvmControlPropertiesEnumerator GetEnumerator()
        {
            if (values == null) return EmptyEnumerator;
            if (keys == null) return new DotvvmControlPropertiesEnumerator(valuesAsDictionary.GetEnumerator());
            return new DotvvmControlPropertiesEnumerator(this.keysAsArray, this.valuesAsArray);
        }

        IEnumerator<KeyValuePair<DotvvmProperty, object?>> IEnumerable<KeyValuePair<DotvvmProperty, object?>>.GetEnumerator() =>
            GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static DotvvmControlPropertiesEnumerator EmptyEnumerator = new DotvvmControlPropertiesEnumerator(new DotvvmProperty[0], new object[0]);

        public bool Remove(DotvvmProperty key)
        {
            if (!Contains(key)) return false;
            if (this.keys == null && valuesAsDictionary != null)
            {
                return valuesAsDictionary.Remove(key);
            }

            // move from read-only struct to mutable struct
            {
                var keysTmp = this.keysAsArray;
                var valuesTmp = this.valuesAsArray;
                var d = new Dictionary<DotvvmProperty, object?>();

                for (int i = 0; i < keysTmp.Length; i++)
                {
                    if (keysTmp[i] != null && keysTmp[i] != key)
                        d[keysTmp[i]!] = valuesTmp[i];
                }
                this.valuesAsDictionary = d;
                this.keys = null;
                return true;
            }
        }

        private static object? CloneValue(object? value)
        {
            if (value is DotvvmBindableObject bindableObject)
                return bindableObject.CloneControl();
            return null;
        }

        public int Count()
        {
            if (this.values == null) return 0;
            if (this.keys == null)
                return this.valuesAsDictionary.Count;
            int count = 0;
            for (int i = 0; i < this.keysAsArray.Length; i++)
            {
                // get rid of a branch which would be almost always misspredicted
                var x = this.keysAsArray[i] is not null;
                count += Unsafe.As<bool, byte>(ref x);
            }
            return count;
        }

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
                    newDict.valuesAsDictionary = new Dictionary<DotvvmProperty, object?>(dictionary);
                    foreach (var (key, value) in dictionary)
                        if (CloneValue(value) is {} newValue)
                            newDict.valuesAsDictionary[key] = newValue;
                    return;
                }
                // move to immutable version if it's reasonably small. It will be probably cloned multiple times again
                var properties = new DotvvmProperty[dictionary.Count];
                var values = new object?[properties.Length];
                int j = 0;
                foreach (var x in this.valuesAsDictionary)
                {
                    (properties[j], values[j]) = x;
                    j++;
                }
                Array.Sort(properties, values, PropertyImmutableHashtable.DotvvmPropertyComparer.Instance);
                (this.hashSeed, this.keysAsArray, this.valuesAsArray) = PropertyImmutableHashtable.CreateTableWithValues(properties, values);
            }

            newDict = this;
            for (int i = 0; i < newDict.valuesAsArray.Length; i++)
            {
                if (CloneValue(newDict.valuesAsArray[i]) is {} newValue)
                {
                    // clone the array if we didn't do that already
                    if (newDict.values == this.values)
                        newDict.values = this.valuesAsArray.Clone();

                    newDict.valuesAsArray[i] = newValue;
                }
            }
        }
    }

    public struct DotvvmControlPropertiesEnumerator : IEnumerator<KeyValuePair<DotvvmProperty, object?>>
    {
        private DotvvmProperty?[]? keys;
        private object?[]? values;
        private int index;
        private Dictionary<DotvvmProperty, object?>.Enumerator dictEnumerator;

        internal DotvvmControlPropertiesEnumerator(DotvvmProperty?[] keys, object?[] values)
        {
            this.keys = keys;
            this.values = values;
            this.index = -1;
            dictEnumerator = default;
        }

        internal DotvvmControlPropertiesEnumerator(in Dictionary<DotvvmProperty, object?>.Enumerator e)
        {
            this.keys = null;
            this.values = null;
            this.index = 0;
            this.dictEnumerator = e;
        }

        public KeyValuePair<DotvvmProperty, object?> Current => keys == null ? dictEnumerator.Current : new KeyValuePair<DotvvmProperty, object?>(keys[index]!, values![index]);

        object IEnumerator.Current => this.Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (keys == null)
                return dictEnumerator.MoveNext();
            while (++index < keys.Length && keys[index] == null) { }
            return index < keys.Length;
        }

        public void Reset()
        {
            if (keys == null)
                ((IEnumerator)dictEnumerator).Reset();
            else index = -1;
        }
    }

    public readonly struct DotvvmPropertyDictionary : IDictionary<DotvvmProperty, object?>
    {
        private readonly DotvvmBindableObject control;

        public DotvvmPropertyDictionary(DotvvmBindableObject control)
        {
            this.control = control;
        }

        public object? this[DotvvmProperty key] { get => control.properties.GetOrThrow(key); set => control.properties.Set(key, value); }

        public KeysCollection Keys => new KeysCollection(control);
        ICollection<DotvvmProperty> IDictionary<DotvvmProperty, object?>.Keys => Keys;

        public ValuesCollection Values => new ValuesCollection(control);
        ICollection<object?> IDictionary<DotvvmProperty, object?>.Values => Values;

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
            foreach (var x in control.properties)
            {
                array[arrayIndex++] = x;
            }
        }

        public DotvvmControlPropertiesEnumerator GetEnumerator() => control.properties.GetEnumerator();

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


        public readonly struct KeysCollection : ICollection<DotvvmProperty>, IReadOnlyCollection<DotvvmProperty>
        {
            private readonly DotvvmBindableObject control;

            public KeysCollection(DotvvmBindableObject control) { this.control = control; }
            public int Count => control.properties.Count();

            public bool IsReadOnly => true;
            public void Add(DotvvmProperty item) => throw new NotSupportedException("Adding a property without value doesn't make sense");
            public void Clear() => throw new NotSupportedException("Explicitly use control.Properties.Clear() instead.");
            public bool Contains(DotvvmProperty item) => control.properties.Contains(item);
            public void CopyTo(DotvvmProperty[] array, int arrayIndex)
            {
                foreach (var x in control.properties)
                {
                    array[arrayIndex++] = x.Key;
                }
            }
            public IEnumerator<DotvvmProperty> GetEnumerator()
            {
                foreach (var x in control.properties)
                {
                    yield return x.Key;
                }
            }
            public bool Remove(DotvvmProperty item) => throw new NotSupportedException("Explicitly use control.Properties.Remove() instead.");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public struct ValuesCollection : ICollection<object?>
        {
            private readonly DotvvmBindableObject control;

            public ValuesCollection(DotvvmBindableObject control) { this.control = control; }
            public int Count => control.properties.Count();

            public bool IsReadOnly => true;
            public void Add(object? item) => throw new NotSupportedException("Adding a value without property doesn't make sense");
            public void Clear() => throw new NotSupportedException("Explicitly use control.Properties.Clear() instead.");
            public bool Contains(object? item)
            {
                foreach (var x in control.properties)
                {
                    if (Object.Equals(x.Value, item))
                        return true;
                }
                return false;
            }
            public void CopyTo(object?[] array, int arrayIndex)
            {
                foreach (var x in control.properties)
                {
                    array[arrayIndex++] = x.Value;
                }
            }
            public IEnumerator<object?> GetEnumerator()
            {
                foreach (var x in control.properties)
                {
                    yield return x.Value;
                }
            }
            public bool Remove(object? item) => throw new NotSupportedException("Explicitly use control.Properties.Remove() instead.");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
