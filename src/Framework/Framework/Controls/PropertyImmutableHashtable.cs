#define Vectorize // for easier testing on without old Framework
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
using System.Security;
using System.Diagnostics.CodeAnalysis;

#if NET6_0_OR_GREATER && Vectorize
using System.Runtime.Intrinsics;
#endif

#if NET7_0_OR_GREATER
using UnreachableException = System.Diagnostics.UnreachableException;
#else
using UnreachableException = System.Exception;
#endif

namespace DotVVM.Framework.Controls
{
    internal static class PropertyImmutableHashtable
    {
        /// <summary> Up to this size, we don't bother with hashing as all keys can just be compared and searched with a single AVX instruction. </summary>
        public const int AdhocTableSize = 8;
        public const int AdhocLargeTableSize = 16;
        public const int MaxArrayTableSize = 16;

        // General implementation notes:
        // * The most heavily used methods are specialized to 8-wide and 16-wide arrays,
        //   others might work with variable size, but divisibility by 8 is always assumed.
        //   8 is the number of property IDs which fit into a 256-bit AVX register, which
        //   is currently available in most CPUs.
        // * We check for HW support of 128-bit registers, but then use 256-bit - this is on purpose:
        //   Vector256 automatically falls back onto 128, if support for it isn't available, which
        //   is still advantageous over 8 scalar operations. Main reason is that we can use
        //   ARM CPUs currently have 128-bit wide SIMD registers, but can generally issue
        //   more vector instructions in parallel (so the compute power is similar to x86).

        private const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
        private const MethodImplOptions NoInlining = MethodImplOptions.NoInlining;


        [MethodImpl(Inline)]
        private static bool ContainsKey8(ref DotvvmPropertyId keys, DotvvmPropertyId p)
        {
#if NET8_0_OR_GREATER && Vectorize
            if (Vector128.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                return Vector256.EqualsAny(v, Vector256.Create(p.Id));
            }
#endif
            return
                Unsafe.Add(ref keys, 0).Id == p.Id |
                Unsafe.Add(ref keys, 1).Id == p.Id |
                Unsafe.Add(ref keys, 2).Id == p.Id |
                Unsafe.Add(ref keys, 3).Id == p.Id |
                Unsafe.Add(ref keys, 4).Id == p.Id |
                Unsafe.Add(ref keys, 5).Id == p.Id |
                Unsafe.Add(ref keys, 6).Id == p.Id |
                Unsafe.Add(ref keys, 7).Id == p.Id;
        }

        [MethodImpl(Inline)]
        public static bool ContainsKey8(DotvvmPropertyId[] keys, DotvvmPropertyId p)
        {
            Debug.Assert(keys.Length == AdhocTableSize);
            return ContainsKey8(ref MemoryMarshal.GetArrayDataReference(keys), p);
        }

        [MethodImpl(Inline)]
        private static bool ContainsKey16(ref DotvvmPropertyId keys, DotvvmPropertyId p)
        {
#if NET8_0_OR_GREATER && Vectorize
            if (Vector128.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                // most likely, dictionary does not contain the value, so we will need to read the second vector in almost all cases
                var v1 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                var v2 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keys, 8)));
                return Vector256.EqualsAny(v1, Vector256.Create(p.Id)) || Vector256.EqualsAny(v2, Vector256.Create(p.Id));
            }
#endif
            return ContainsKey8(ref keys, p) || ContainsKey8(ref Unsafe.Add(ref keys, 8), p);
        }

        public static bool ContainsKey16(DotvvmPropertyId[] keys, DotvvmPropertyId p)
        {
            Debug.Assert(keys.Length == AdhocLargeTableSize);
            return ContainsKey16(ref MemoryMarshal.GetArrayDataReference(keys), p);
        }

        [MethodImpl(Inline)]
        private static int FindSlot8(ref DotvvmPropertyId keys, DotvvmPropertyId p)
        {
#if NET8_0_OR_GREATER && Vectorize
            if (Vector128.IsHardwareAccelerated)
            {
                Debug.Assert(Vector256<uint>.Count == AdhocTableSize);
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                var eq = Vector256.Equals(v, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
                else
                {
                    return -1;
                }
            }
#endif
            if (Unsafe.Add(ref keys, 0).Id == p.Id) return 0;
            if (Unsafe.Add(ref keys, 1).Id == p.Id) return 1;
            if (Unsafe.Add(ref keys, 2).Id == p.Id) return 2;
            if (Unsafe.Add(ref keys, 3).Id == p.Id) return 3;
            if (Unsafe.Add(ref keys, 4).Id == p.Id) return 4;
            if (Unsafe.Add(ref keys, 5).Id == p.Id) return 5;
            if (Unsafe.Add(ref keys, 6).Id == p.Id) return 6;
            if (Unsafe.Add(ref keys, 7).Id == p.Id) return 7;

            return -1;
        }

        [MethodImpl(Inline)]
        public static int FindSlot8(DotvvmPropertyId[] keys, DotvvmPropertyId p)
        {
            Debug.Assert(keys.Length == AdhocTableSize);
            return FindSlot8(ref MemoryMarshal.GetArrayDataReference(keys), p);
        }

        [MethodImpl(Inline)]
        private static int FindSlot16(ref DotvvmPropertyId keys, DotvvmPropertyId p)
        {
#if NET8_0_OR_GREATER && Vectorize
            if (Vector128.IsHardwareAccelerated)
            {
                var v1 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                var v2 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keys, 8)));
                var eq1 = Vector256.Equals<uint>(v1, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                var eq2 = Vector256.Equals<uint>(v2, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                var eq = eq1 | (eq2 << 8);
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
                else
                {
                    return -1;
                }
            }
#endif
            var ix = FindSlot8(ref keys, p);
            if (ix >= 0) return ix;

            ix = FindSlot8(ref Unsafe.Add(ref keys, 8), p);
            if (ix >= 0) return ix + 8;

            return -1;
        }

        [MethodImpl(Inline)]
        public static int FindSlot16(DotvvmPropertyId[] keys, DotvvmPropertyId p)
        {
            Debug.Assert(keys.Length == AdhocLargeTableSize);
            return FindSlot16(ref MemoryMarshal.GetArrayDataReference(keys), p);
        }

        public static int FindSlot(DotvvmPropertyId[] keys, DotvvmPropertyId p)
        {
            if (keys.Length == AdhocTableSize)
            {
                return FindSlot8(keys, p);
            }
            else if (keys.Length == AdhocLargeTableSize)
            {
                return FindSlot16(keys, p);
            }
            else
            {
                throw new ArgumentException("Keys must have 8 or 16 elements.", nameof(keys));
            }
        }

        [MethodImpl(Inline)]
        private static int FindSlotOrFree8(ref DotvvmPropertyId keys, DotvvmPropertyId p, out bool exists)
        {
#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated)
            {
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                var eq = Vector256.Equals(v, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                exists = eq != 0;
                if (eq != 0)
                {
                    return BitOperations.TrailingZeroCount(eq);
                }
                var empty = Vector256.Equals(v, Vector256<uint>.Zero).ExtractMostSignificantBits();
                if (empty != 0)
                {
                    return BitOperations.TrailingZeroCount(empty);
                }

                return -1;
            }
#endif      
            exists = true;
            if (Unsafe.Add(ref keys, 0).Id == p.Id) return 0;
            if (Unsafe.Add(ref keys, 1).Id == p.Id) return 1;
            if (Unsafe.Add(ref keys, 2).Id == p.Id) return 2;
            if (Unsafe.Add(ref keys, 3).Id == p.Id) return 3;
            if (Unsafe.Add(ref keys, 4).Id == p.Id) return 4;
            if (Unsafe.Add(ref keys, 5).Id == p.Id) return 5;
            if (Unsafe.Add(ref keys, 6).Id == p.Id) return 6;
            if (Unsafe.Add(ref keys, 7).Id == p.Id) return 7;
            exists = false;
            if (Unsafe.Add(ref keys, 0).Id == 0) return 0;
            if (Unsafe.Add(ref keys, 1).Id == 0) return 1;
            if (Unsafe.Add(ref keys, 2).Id == 0) return 2;
            if (Unsafe.Add(ref keys, 3).Id == 0) return 3;
            if (Unsafe.Add(ref keys, 4).Id == 0) return 4;
            if (Unsafe.Add(ref keys, 5).Id == 0) return 5;
            if (Unsafe.Add(ref keys, 6).Id == 0) return 6;
            if (Unsafe.Add(ref keys, 7).Id == 0) return 7;

            return -1;
        }

        [MethodImpl(Inline)]
        private static int FindSlotOrFree16(ref DotvvmPropertyId keys, DotvvmPropertyId p, out bool exists)
        {
#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated)
            {
                var v1 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                var v2 = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keys, 8)));
                var eq1 = Vector256.Equals(v1, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                var eq2 = Vector256.Equals(v2, Vector256.Create(p.Id)).ExtractMostSignificantBits();
                var eq = eq1 | (eq2 << 8);
                exists = eq != 0;
                if (eq != 0)
                    return BitOperations.TrailingZeroCount(eq);

                var empty1 = Vector256.Equals(v1, Vector256<uint>.Zero).ExtractMostSignificantBits();
                var empty2 = Vector256.Equals(v2, Vector256<uint>.Zero).ExtractMostSignificantBits();
                var empty = empty1 | (empty2 << 8);
                if (empty != 0)
                    return BitOperations.TrailingZeroCount(empty);

                return -1;
            }
#endif
            var ix = FindSlot16(ref keys, p);
            exists = ix >= 0;
            if (ix >= 0)
            {
                return ix;
            }
            ix = FindSlot16(ref keys, 0);
            if (ix >= 0)
            {
                return ix;
            }

            return -1;
        }


        [MethodImpl(Inline)]
        public static int FindSlotOrFree8(DotvvmPropertyId[] keys, DotvvmPropertyId p, out bool exists) =>
            FindSlotOrFree8(ref MemoryMarshal.GetArrayDataReference(keys), p, out exists);
        [MethodImpl(NoInlining)]
        public static int FindSlotOrFree16(DotvvmPropertyId[] keys, DotvvmPropertyId p, out bool exists) =>
            FindSlotOrFree16(ref MemoryMarshal.GetArrayDataReference(keys), p, out exists);

        public static ushort FindGroupBitmap(ref DotvvmPropertyId keys, int length, ushort groupId)
        {
            Debug.Assert(length is AdhocTableSize or AdhocLargeTableSize);

            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId; // groupId ^ 0x8000

            ushort bitmap = 0;
#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated)
            {
                var v1 = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                bitmap = (ushort)Vector256.Equals(v1 >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits();
                if (length == 16)
                {
                    var v2 = Unsafe.ReadUnaligned<Vector256<uint>>(in Unsafe.As<DotvvmPropertyId, byte>(ref Unsafe.Add(ref keys, 8)));
                    bitmap |= (ushort)(Vector256.Equals(v2 >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits() << 8);
                }
                return bitmap;
            }
#endif
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 0).TypeId == idPrefix) << 0);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 1).TypeId == idPrefix) << 1);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 2).TypeId == idPrefix) << 2);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 3).TypeId == idPrefix) << 3);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 4).TypeId == idPrefix) << 4);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 5).TypeId == idPrefix) << 5);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 6).TypeId == idPrefix) << 6);
            bitmap |= (ushort)(BoolToInt(Unsafe.Add(ref keys, 7).TypeId == idPrefix) << 7);
            if (length == 16)
            {
                bitmap |= (ushort)(FindGroupBitmap(ref Unsafe.Add(ref keys, 8), AdhocTableSize, groupId) << 8);
            }
            return bitmap;
        }

        public static ushort FindGroupBitmap(DotvvmPropertyId[] keys, ushort groupId)
        {
            return FindGroupBitmap(ref MemoryMarshal.GetArrayDataReference(keys), keys.Length, groupId);
        }

        public static bool ContainsPropertyGroup(DotvvmPropertyId[] keys, ushort groupId)
        {
            Debug.Assert(keys.Length % Vector256<uint>.Count == 0);
            Debug.Assert(keys.Length >= AdhocTableSize);
            Debug.Assert(keys.Length % 8 == 0);

            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId;
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);

#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated)
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
#endif
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].TypeId == idPrefix)
                {
                    return true;
                }
            }
            return false;
        }

        public static int Count(DotvvmPropertyId[] keys)
        {
            Debug.Assert(keys.Length % Vector256<uint>.Count == 0);
            Debug.Assert(keys.Length >= AdhocTableSize);

            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
            Debug.Assert(keys.Length % 8 == 0);

#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated)
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
#endif
            int count = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                count += BoolToInt(keys[i].Id == 0);
            }
            return count;
        }

        [MethodImpl(Inline)]
        private static int CountPropertyGroup8(ref DotvvmPropertyId keys, ushort groupId)
        {
            ushort idPrefix = DotvvmPropertyId.CreatePropertyGroupId(groupId, 0).TypeId;
            ref var keysInts = ref Unsafe.As<DotvvmPropertyId, uint>(ref keys);

#if NET8_0_OR_GREATER && Vectorize
            if (Vector128.IsHardwareAccelerated)
            {
                var v = Unsafe.ReadUnaligned<Vector256<uint>>(ref Unsafe.As<DotvvmPropertyId, byte>(ref keys));
                return BitOperations.PopCount(Vector256.Equals(v >> 16, Vector256.Create((uint)idPrefix)).ExtractMostSignificantBits());
            }
#endif
            int count = 0;
            count += BoolToInt(Unsafe.Add(ref keysInts, 0) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 1) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 2) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 3) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 4) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 5) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 6) >> 16 == idPrefix);
            count += BoolToInt(Unsafe.Add(ref keysInts, 7) >> 16 == idPrefix);
            return count;
        }

        [MethodImpl(Inline)]
        public static int CountPropertyGroup8(DotvvmPropertyId[] keys, ushort groupId)
        {
            Debug.Assert(keys.Length == 8);
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
            return CountPropertyGroup8(ref keysRef, groupId);
        }

        public static int CountPropertyGroup(DotvvmPropertyId[] keys, ushort groupId)
        {
            Debug.Assert(keys.Length % 8 == 0);
            ref var keysRef = ref MemoryMarshal.GetArrayDataReference(keys);

            int count = 0;
            for (int i = 0; i < keys.Length; i += 8)
            {
                count += CountPropertyGroup8(ref Unsafe.Add(ref keysRef, i), groupId);
            }
            return count;
        }

        [MethodImpl(Inline)]
        private static byte BoolToInt(bool x) =>
#if NET8_0_OR_GREATER // JIT can finally optimize this to branchless code
            x ? (byte)1 : (byte)0;
#else
            Unsafe.As<bool, byte>(ref x);
#endif

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

        [MethodImpl(Inline)]
        public static void Assert([DoesNotReturnIf(false)] bool condition)
        {
            if (!condition)
                Fail();
        }

        [DoesNotReturn]
        [MethodImpl(NoInlining)]
        public static void Fail() => throw new UnreachableException("Assertion failed in DotVVM property dictionary. This is a serious bug, please report it.");

        [DoesNotReturn]
        [MethodImpl(NoInlining)]
        public static T Fail<T>() => throw new UnreachableException("Assertion failed in DotVVM property dictionary. This is a serious bug, please report it.");

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
            if (keys.Length > 16)
            {
                throw new ArgumentException("Keys must have at most 16 elements.", nameof(keys));
            }

            // make sure that all tables have the same keys so that they don't take much RAM (and remain in cache and make things go faster)
            return tableCache.GetOrAdd(keys, static keys => {
                // pad result to 8 or 16 elements
                var result = new DotvvmPropertyId[keys.Length <= 8 ? 8 : 16];
                Array.Copy(keys, result, keys.Length);
                return (0, result);
            });
        }

        public static (uint hashSeed, DotvvmPropertyId[] keys, T[] valueTable) CreateTableWithValues<T>(DotvvmPropertyId[] properties, T[] values)
        {
            if (properties.Length != values.Length)
                throw new ArgumentException("Properties and values must have the same length.", nameof(properties));
            if (properties.Length > 16)
                throw new ArgumentException("Properties and values must have at most 16 elements, otherwise create a dictionary.", nameof(properties));

            

            var (hashSeed, keys) = BuildTable(properties);
            var valueTable = new T[keys.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                valueTable[FindSlot(keys, properties[i])] = values[i];
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
            if (properties.Length > MaxArrayTableSize)
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
                return (obj) => obj.properties.AssignBulk(keys, valueTable, false, false);
            }
        }

        public static void SetValuesToDotvvmControl(DotvvmBindableObject obj, DotvvmPropertyId[] properties, object?[] values, bool ownsKeys, bool ownsValues)
        {
            obj.properties.AssignBulk(properties, values, ownsKeys, ownsValues);
        }

        public static void SetValuesToDotvvmControl(DotvvmBindableObject obj, Dictionary<DotvvmPropertyId, object?> values, bool owns)
        {
            obj.properties.AssignBulk(values, owns);
        }
    }
}
