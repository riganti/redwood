using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Hosting.ErrorPages;
using RecordExceptions;

#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

namespace DotVVM.Framework.Controls
{
    internal static class PropertyImmutableHashtable
    {
        /// <summary> Up to this size, we don't bother with hashing as all keys can just be compared and searched with a single AVX instruction. </summary>
        public const int AdhocTableSize = 8;
        public const int ArrayMultipleSize = 8;

        static int HashCombine(int a, int b) => HashCode.Combine(a, b);

        public static bool ContainsKey(DotvvmPropertyId[] keys, uint flags, DotvvmPropertyId p)
        {
#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref MemoryMarshal.GetArrayDataReference((Array)keys));
                if (Vector256.EqualsAny(v, Vector256.Create(p.Id)))
                {
                    return true;
                }
            }
#else
            if (false) { }
#endif
            else
            {
                if (keys[7].Id == p.Id || keys[6].Id == p.Id || keys[5].Id == p.Id || keys[4].Id == p.Id || keys[3].Id == p.Id || keys[2].Id == p.Id || keys[1].Id == p.Id || keys[0] == p)
                {
                    return true;
                }
            }

            if (keys.Length == AdhocTableSize)
            {
                return false;
            }

            var hashSeed = flags & 0x3FFF_FFFF;
            var lengthMap = keys.Length - 1; // trims the hash to be in bounds of the array
            var hash = HashCombine(p.GetHashCode(), (int)hashSeed) & lengthMap;

            var i1 = hash & -2; // hash with last bit == 0 (-2 is something like ff...fe because two's complement)
            var i2 = hash | 1; // hash with last bit == 1

            return keys[i1].Id == p.Id | keys[i2].Id == p.Id;
        }

        public static int FindSlot(DotvvmPropertyId[] keys, uint flags, DotvvmPropertyId p)
        {
#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref MemoryMarshal.GetArrayDataReference((Array)keys));
                var eq = Vector256.Equals(v, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
            }
#else
            if (false) { }
#endif
            else
            {
                if (keys[7].Id == p.Id) return 7;
                if (keys[6].Id == p.Id) return 6;
                if (keys[5].Id == p.Id) return 5;
                if (keys[4].Id == p.Id) return 4;
                if (keys[3].Id == p.Id) return 3;
                if (keys[2].Id == p.Id) return 2;
                if (keys[1].Id == p.Id) return 1;
                if (keys[0].Id == p.Id) return 0;
            }

            if (keys.Length == AdhocTableSize)
            {
                return -1;
            }

            var lengthMap = keys.Length - 1; // trims the hash to be in bounds of the array
            var hashSeed = flags & 0x3FFF_FFFF;
            var hash = HashCombine(p.GetHashCode(), (int)hashSeed) & lengthMap;

            var i1 = hash & -2; // hash with last bit == 0 (-2 is something like ff...fe because two's complement)
            var i2 = hash | 1; // hash with last bit == 1

            if (keys[i1].Id == p.Id) return i1;
            if (keys[i2].Id == p.Id) return i2;
            return -1;
        }

        public static int FindSlotOrFree(DotvvmPropertyId[] keys, uint flags, DotvvmPropertyId p, out bool exists)
        {
            int free = -1;
            exists = true;

#if NET8_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref MemoryMarshal.GetArrayDataReference((Array)keys));
                var eq = Vector256.Equals(v, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
                var eq0 = Vector256.Equals(v, Vector256.Create(0u)).ExtractMostSignificantBits();
                if (eq0 != 0)
                {
                    free = BitOperations.TrailingZeroCount(eq0);
                }
            }
#else
            if (false) { }
#endif
            else
            {
                if (keys[7].Id == p.Id) return 7;
                if (keys[6].Id == p.Id) return 6;
                if (keys[5].Id == p.Id) return 5;
                if (keys[4].Id == p.Id) return 4;
                if (keys[3].Id == p.Id) return 3;
                if (keys[2].Id == p.Id) return 2;
                if (keys[1].Id == p.Id) return 1;
                if (keys[0].Id == p.Id) return 0;
                if (keys[7].Id == 0) free = 7;
                else if (keys[6].Id == 0) free = 6;
                else if (keys[5].Id == 0) free = 5;
                else if (keys[4].Id == 0) free = 4;
                else if (keys[3].Id == 0) free = 3;
                else if (keys[2].Id == 0) free = 2;
                else if (keys[1].Id == 0) free = 1;
                else if (keys[0].Id == 0) free = 0;
            }

            if (keys.Length == 8)
            {
                exists = false;
                return free;
            }

            var lengthMap = keys.Length - 1; // trims the hash to be in bounds of the array
            var hashSeed = flags & 0x3FFF_FFFF;
            var hash = HashCombine(p.GetHashCode(), (int)hashSeed) & lengthMap;

            var i1 = hash & -2; // hash with last bit == 0 (-2 is something like ff...fe because two's complement)
            var i2 = hash | 1; // hash with last bit == 1
    
            if (keys[i1].Id == p.Id) return i1;
            if (keys[i2].Id == p.Id) return i2;
            exists = false;
            if (keys[i1].Id == 0) return i1;
            if (keys[i2].Id == 0) return i2;
            return free;
        }


        public static int FindFreeAdhocSlot(DotvvmPropertyId[] keys)
        {
            Debug.Assert(keys.Length >= AdhocTableSize);
#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref MemoryMarshal.GetArrayDataReference((Array)keys));
                var eq = Vector256.Equals(v, Vector256.Create(0u)).ExtractMostSignificantBits();
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
            }
#else
            if (false) { }
#endif
            else
            {
                if (keys[7].Id == 0) return 7;
                if (keys[6].Id == 0) return 6;
                if (keys[5].Id == 0) return 5;
                if (keys[4].Id == 0) return 4;
                if (keys[3].Id == 0) return 3;
                if (keys[2].Id == 0) return 2;
                if (keys[1].Id == 0) return 1;
                if (keys[0].Id == 0) return 0;
            }
            return -1;
        }

        public static ushort FindGroupInNext16Slots(DotvvmPropertyId[] keys, uint startIndex, ushort groupId)
        {
            Debug.Assert(keys.Length % ArrayMultipleSize == 0);
            Debug.Assert(keys.Length >= AdhocTableSize);

            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId;
            ushort bitmap = 0;
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);

#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                var v1 = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keysRef, (int)startIndex)));
                bitmap = (ushort)Vector256.Equals(v1 >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits();
                if (keys.Length > startIndex + 8)
                {
                    var v2 = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keysRef, (int)startIndex + 8)));
                    bitmap |= (ushort)(Vector256.Equals(v2 >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits() << 8);
                }
            }
#else
            if (false) { }
#endif
            else
            {
                bitmap |= (ushort)((keys[startIndex + 7].TypeId == idPrefix ? 1 : 0) << 7);
                bitmap |= (ushort)((keys[startIndex + 6].TypeId == idPrefix ? 1 : 0) << 6);
                bitmap |= (ushort)((keys[startIndex + 5].TypeId == idPrefix ? 1 : 0) << 5);
                bitmap |= (ushort)((keys[startIndex + 4].TypeId == idPrefix ? 1 : 0) << 4);
                bitmap |= (ushort)((keys[startIndex + 3].TypeId == idPrefix ? 1 : 0) << 3);
                bitmap |= (ushort)((keys[startIndex + 2].TypeId == idPrefix ? 1 : 0) << 2);
                bitmap |= (ushort)((keys[startIndex + 1].TypeId == idPrefix ? 1 : 0) << 1);
                bitmap |= (ushort)((keys[startIndex + 0].TypeId == idPrefix ? 1 : 0) << 0);
                if (keys.Length > startIndex + 8)
                {
                    bitmap |= (ushort)((keys[startIndex + 15].TypeId == idPrefix ? 1 : 0) << 15);
                    bitmap |= (ushort)((keys[startIndex + 14].TypeId == idPrefix ? 1 : 0) << 14);
                    bitmap |= (ushort)((keys[startIndex + 13].TypeId == idPrefix ? 1 : 0) << 13);
                    bitmap |= (ushort)((keys[startIndex + 12].TypeId == idPrefix ? 1 : 0) << 12);
                    bitmap |= (ushort)((keys[startIndex + 11].TypeId == idPrefix ? 1 : 0) << 11);
                    bitmap |= (ushort)((keys[startIndex + 10].TypeId == idPrefix ? 1 : 0) << 10);
                    bitmap |= (ushort)((keys[startIndex + 9].TypeId == idPrefix ? 1 : 0) << 9);
                    bitmap |= (ushort)((keys[startIndex + 8].TypeId == idPrefix ? 1 : 0) << 8);
                }
            }
            return bitmap;
        }

        public static bool ContainsPropertyGroup(DotvvmPropertyId[] keys, ushort groupId)
        {
            Debug.Assert(keys.Length % ArrayMultipleSize == 0 && Vector256<uint>.Count == ArrayMultipleSize);
            Debug.Assert(keys.Length >= AdhocTableSize);

            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId;
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
            Debug.Assert(keys.Length % 8 == 0);

#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                for (int i = 0; i < keys.Length; i += 8)
                {
                    var v = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keysRef, i)));
                    if (Vector256.EqualsAny(v >> 16, Vector256.Create((uint)idPrefix)))
                    {
                        return true;
                    }
                }
                return false;
            }
#else
            if (false) { }
#endif
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].TypeId == idPrefix)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static int Count(DotvvmPropertyId[] keys)
        {
            Debug.Assert(keys.Length % ArrayMultipleSize == 0 && Vector256<uint>.Count == ArrayMultipleSize);
            Debug.Assert(keys.Length >= AdhocTableSize);

            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
            Debug.Assert(keys.Length % 8 == 0);

#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                int zeroCount = 0;
                for (int i = 0; i < keys.Length; i += Vector256<uint>.Count)
                {
                    var v = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keysRef, i)));
                    var isZero = Vector256.Equals(v, Vector256.Create(0u)).ExtractMostSignificantBits();
                    zeroCount += BitOperations.PopCount(isZero);
                }
                return keys.Length - zeroCount;
            }
#else
            if (false) { }
#endif
            else
            {
                int count = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    count += BoolToInt(keys[i].Id == 0);
                }
                return count;
            }

        }

        public static int CountPropertyGroup(DotvvmPropertyId[] keys, ushort groupId)
        {
            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId;
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
            Debug.Assert(keys.Length % ArrayMultipleSize == 0);

            int count = 0;

#if NET7_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated)
            {
                for (int i = 0; i < keys.Length; i += Vector256<uint>.Count)
                {
                    var v = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keysRef, i)));
                    count += BitOperations.PopCount(Vector256.Equals(v >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits());
                }
            }
#else
            if (false) { }
#endif
            else
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    count += BoolToInt(keys[i].TypeId == idPrefix);
                }
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte BoolToInt(bool x) => Unsafe.As<bool, byte>(ref x);

        static ConcurrentDictionary<DotvvmPropertyId[], (uint, DotvvmPropertyId[])> tableCache = new(new EqCmp());

        class EqCmp : IEqualityComparer<DotvvmPropertyId[]>
        {
            public bool Equals(DotvvmPropertyId[]? x, DotvvmPropertyId[]? y)
            {
                if (object.ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                if (x.Length != y.Length) return false;

                for (int i = 0; i < x.Length; i++)
                    if (x[i] != y[i]) return false;
                return true;
            }

            public int GetHashCode(DotvvmPropertyId[] obj)
            {
                var h = obj.Length;
                foreach (var i in obj)
                    h = (i, h).GetHashCode();
                return h;
            }
        }

        // Some primes. Numbers not divisible by 2 should help shuffle the table in a different way every time.
        public static uint[] hashSeeds = [0, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503, 509, 521, 523, 541];

        private static bool IsOrderedWithoutDuplicatesAndZero(DotvvmPropertyId[] keys)
        {
            uint last = 0;
            foreach (var k in keys)
            {
                if (k.Id <= last) return false;
                if (k.MemberId == 0) return false;
                last = k.Id;
            }
            return true;
        }

        public static (uint hashSeed, DotvvmPropertyId[] keys) BuildTable(DotvvmPropertyId[] keys)
        {
            if (!IsOrderedWithoutDuplicatesAndZero(keys))
            {
                throw new ArgumentException("Keys must be ordered, without duplicates and without zero.", nameof(keys));
            }

            // make sure that all tables have the same keys so that they don't take much RAM (and remain in cache and make things go faster)
            return tableCache.GetOrAdd(keys, static keys => {
                if (keys.Length <= 8)
                {
                    // just pad them to make things regular
                    var result = new DotvvmPropertyId[8];
                    Array.Copy(keys, result, keys.Length);
                    return (0, result);
                }
                else
                {
                    // first try closest size of power two
                    var size = 1 << (int)Math.Ceiling(Math.Log(keys.Length, 2));

                    // all vector optimizations assume length at least 8
                    size = Math.Max(size, AdhocTableSize);
                    Debug.Assert(size % ArrayMultipleSize == 0);

                    while(true)
                    {
                        Debug.Assert((size & (size - 1)) == 0);
                        foreach (var hashSeed in hashSeeds)
                        {
                            var result = TryBuildTable(keys, size, hashSeed);
                            if (result != null)
                            {
                                Debug.Assert(TestTableCorrectness(keys, hashSeed, result));
                                return (hashSeed, result);
                            }
                        }

                        size *= 2;

                        if (size <= 4) throw new CannotMakeHashtableException();
                    }

                }
            });
        }

        static bool TestTableCorrectness(DotvvmPropertyId[] keys, uint hashSeed, DotvvmPropertyId[] table)
        {
            return keys.All(k => FindSlot(table, hashSeed, k) >= 0) && keys.Select(k => FindSlot(table, hashSeed, k)).Distinct().Count() == keys.Length;
        }

        /// <summary> Builds the core of the property hash table. Returns null if the table cannot be built due to collisions. </summary>
        static DotvvmPropertyId[]? TryBuildTable(DotvvmPropertyId[] a, int size, uint hashSeed)
        {
            var t = new DotvvmPropertyId[size];
            var lengthMap = size - 1; // trims the hash to be in bounds of the array
            foreach (var k in a)
            {
                var hash = HashCombine(k.GetHashCode(), (int)hashSeed) & lengthMap;

                var i1 = hash & -2; // hash with last bit == 0 (-2 is something like ff...fe because two's complement)
                var i2 = hash | 1; // hash with last bit == 1

                if (t[i1].IsZero)
                    t[i1] = k;
                else if (t[i2].IsZero)
                    t[i2] = k;
                else return null; // if neither of these slots work, we can't build the table
            }
            return t;
        }

        public static (uint hashSeed, DotvvmPropertyId[] keys, T[] valueTable) CreateTableWithValues<T>(DotvvmPropertyId[] properties, T[] values)
        {
            var (hashSeed, keys) = BuildTable(properties);
            var valueTable = new T[keys.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                valueTable[FindSlot(keys, hashSeed, properties[i])] = values[i];
            }
            return (hashSeed, keys, valueTable);
        }

        public static Action<DotvvmBindableObject> CreateBulkSetter(DotvvmProperty[] properties, object?[] values)
        {
            var ids = properties.Select(p => p.Id).ToArray();
            Array.Sort(ids);
            return CreateBulkSetter(ids, values);
        }
        public static Action<DotvvmBindableObject> CreateBulkSetter(DotvvmPropertyId[] properties, object?[] values)
        {
            if (properties.Length > 30)
            {
                var dict = new Dictionary<DotvvmPropertyId, object?>(capacity: properties.Length);
                for (int i = 0; i < properties.Length; i++)
                {
                    dict[properties[i]] = values[i];
                }
                return (obj) => obj.properties.AssignBulk(dict, false);
            }
            else
            {
                var (hashSeed, keys, valueTable) = CreateTableWithValues(properties, values);
                return (obj) => obj.properties.AssignBulk(keys, valueTable, hashSeed);
            }
        }

        public static void SetValuesToDotvvmControl(DotvvmBindableObject obj, DotvvmPropertyId[] properties, object?[] values, uint flags)
        {
            obj.properties.AssignBulk(properties, values, flags);
        }

        public static void SetValuesToDotvvmControl(DotvvmBindableObject obj, Dictionary<DotvvmPropertyId, object?> values, bool owns)
        {
            obj.properties.AssignBulk(values, owns);
        }

        public record CannotMakeHashtableException: RecordException
        {
            public override string Message => "Cannot make hashtable";
        }
    }
}
