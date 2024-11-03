using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DotVVM.Framework.Configuration
{

    static class FreezableDictionary
    {
        public static void Freeze<K, V>([AllowNull] ref IDictionary<K, V> dict)
            where K : notnull
        {
            if (dict is FreezableDictionary<K, V> freezable)
                freezable.Freeze();
            else
            {
                var comparer = (dict as Dictionary<K, V>)?.Comparer;
                if (dict is object && !dict.IsReadOnly)
                    dict = new FreezableDictionary<K, V>(dict, comparer, frozen: true);
            }
        }
    }
    sealed class FreezableDictionary<K, V> : IDictionary<K, V>, IReadOnlyCollection<KeyValuePair<K, V>>, IReadOnlyDictionary<K, V>
        where K : notnull
    {
        private readonly Dictionary<K, V> dict;
        private bool isFrozen;
        private void ThrowIfFrozen()
        {
            if (isFrozen)
                throw FreezableUtils.Error("dictionary");
        }
        public void Freeze()
        {
            this.isFrozen = true;
        }

        public FreezableDictionary(IEqualityComparer<K>? comparer = null, bool frozen = false)
        {
            dict = new Dictionary<K, V>(comparer);
            isFrozen = frozen;
        }

        public FreezableDictionary(IEnumerable<KeyValuePair<K, V>> items, IEqualityComparer<K>? comparer = null, bool frozen = false)
        {
            dict = items.ToDictionary(a => a.Key, a => a.Value, comparer);
            isFrozen = frozen;
        }

        public void Add(K key, V value)
        {
            ThrowIfFrozen();
            dict.Add(key, value);
        }

        public bool ContainsKey(K key) => dict.ContainsKey(key);
        public bool Remove(K key)
        {
            ThrowIfFrozen();
            return dict.Remove(key);
        }

#pragma warning disable CS8767
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) => dict.TryGetValue(key, out value);
#pragma warning restore CS8767
        public void Add(KeyValuePair<K, V> item)
        {
            ThrowIfFrozen();
            ((IDictionary<K, V>)dict).Add(item);
        }

        public void Clear()
        {
            ThrowIfFrozen();
            dict.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item) => ((IDictionary<K, V>)dict).Contains(item);
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) => ((IDictionary<K, V>)dict).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<K, V> item)
        {
            ThrowIfFrozen();
            return ((IDictionary<K, V>)dict).Remove(item);
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IDictionary<K, V>)dict).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<K, V>)dict).GetEnumerator();

        public V this[K index]
        {
            get => dict[index];
            set { ThrowIfFrozen(); dict[index] = value; }
        }

        public int Count => dict.Count;

        public bool IsReadOnly => isFrozen;

        public ICollection<K> Keys => ((IDictionary<K, V>)dict).Keys.ToArray();

        public ICollection<V> Values => ((IDictionary<K, V>)dict).Values.ToArray();

        IEnumerable<K> IReadOnlyDictionary<K, V>.Keys => Keys;

        IEnumerable<V> IReadOnlyDictionary<K, V>.Values => Values;
    }
}
