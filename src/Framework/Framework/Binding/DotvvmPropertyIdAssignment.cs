using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using FastExpressionCompiler;

namespace DotVVM.Framework.Binding
{
    public readonly struct DotvvmPropertyId: IEquatable<DotvvmPropertyId>, IEquatable<uint>, IComparable<DotvvmPropertyId>
    {
        public readonly uint Id;
        public DotvvmPropertyId(uint id)
        {
            Id = id;
        }

        public DotvvmPropertyId(ushort typeOrGroupId, ushort memberId)
        {
            Id = ((uint)typeOrGroupId << 16) | memberId;
        }

        [MemberNotNullWhen(true, nameof(PropertyGroupInstance), nameof(GroupMemberName))]
        public bool IsPropertyGroup => (int)Id < 0;
        public ushort TypeId => (ushort)(Id >> 16);
        public ushort GroupId => (ushort)((Id >> 16) ^ 0x80_00);
        public ushort MemberId => (ushort)(Id & 0xFFFF);

        /// <summary> Returns true if the property does not have GetValue/SetValue overrides and is not inherited. That means it is sufficient  </summary>
        public bool CanUseFastAccessors
        {
            get
            {
                // properties: we encode this information as the LSB bit of the member ID (i.e. odd/even numbers)
                // property groups: always true, i.e.
                const uint mask = (1u << 31) | (1u);
                const uint targetValue = 1u;
                return (Id & mask) != targetValue;
            }
        }

        public bool IsZero => Id == 0;

        public DotvvmProperty PropertyInstance => DotvvmPropertyIdAssignment.GetProperty(Id) ?? throw new Exception($"Property with ID {Id} not registered.");
        public DotvvmPropertyGroup? PropertyGroupInstance => !IsPropertyGroup ? null : DotvvmPropertyIdAssignment.GetPropertyGroup(GroupId);
        public string? GroupMemberName => !IsPropertyGroup ? null : DotvvmPropertyIdAssignment.GetGroupMemberName(MemberId);

        public bool IsInPropertyGroup(ushort id) => (this.Id >> 16) == ((uint)id | 0x80_00u);

        public static DotvvmPropertyId CreatePropertyGroupId(ushort groupId, ushort memberId) => new DotvvmPropertyId((ushort)(groupId | 0x80_00), memberId);

        public static implicit operator DotvvmPropertyId(uint id) => new DotvvmPropertyId(id);

        public bool Equals(DotvvmPropertyId other) => Id == other.Id;
        public bool Equals(uint other) => Id == other;
        public override bool Equals(object? obj) => obj is DotvvmPropertyId id && Equals(id);
        public override int GetHashCode() => (int)Id;

        public static bool operator ==(DotvvmPropertyId left, DotvvmPropertyId right) => left.Equals(right);
        public static bool operator !=(DotvvmPropertyId left, DotvvmPropertyId right) => !left.Equals(right);

        public override string ToString() => $"PropId={Id}";
        public int CompareTo(DotvvmPropertyId other) => Id.CompareTo(other.Id);
    }

    static class DotvvmPropertyIdAssignment
    {
        const int DEFAULT_PROPERTY_COUNT = 16;
        static readonly ConcurrentDictionary<Type, ushort> typeIds;
        private static readonly object controlTypeRegisterLock = new object();
        private static int controlCounter = 256; // first 256 types are reserved for DotVVM controls
        private static ControlTypeInfo[] controls = new ControlTypeInfo[1024];
        private static readonly object groupRegisterLock = new object();
        private static int groupCounter = 256; // first 256 types are reserved for DotVVM controls
        private static DotvvmPropertyGroup?[] propertyGroups = new DotvvmPropertyGroup[1024];
        private static ulong[] propertyGroupActiveBitmap = new ulong[1024 / 64];
        static readonly ConcurrentDictionary<string, ushort> propertyGroupMemberIds = new(concurrencyLevel: 1, capacity: 256) {
            ["id"] = GroupMembers.id,
            ["class"] = GroupMembers.@class,
            ["style"] = GroupMembers.style,
            ["name"] = GroupMembers.name,
            ["data-bind"] = GroupMembers.databind,
        };
        private static readonly object groupMemberRegisterLock = new object();
        static string?[] propertyGroupMemberNames = new string[1024];

        static DotvvmPropertyIdAssignment()
        {
            foreach (var n in propertyGroupMemberIds)
            {
                propertyGroupMemberNames[n.Value] = n.Key;
            }

            typeIds = new() {
                [typeof(DotvvmBindableObject)] = TypeIds.DotvvmBindableObject,
                [typeof(DotvvmControl)] = TypeIds.DotvvmControl,
                [typeof(HtmlGenericControl)] = TypeIds.HtmlGenericControl,
                [typeof(RawLiteral)] = TypeIds.RawLiteral,
                [typeof(Literal)] = TypeIds.Literal,
                [typeof(ButtonBase)] = TypeIds.ButtonBase,
                [typeof(Button)] = TypeIds.Button,
                [typeof(LinkButton)] = TypeIds.LinkButton,
                [typeof(TextBox)] = TypeIds.TextBox,
                [typeof(RouteLink)] = TypeIds.RouteLink,
                [typeof(CheckableControlBase)] = TypeIds.CheckableControlBase,
                [typeof(CheckBox)] = TypeIds.CheckBox,
                [typeof(Validator)] = TypeIds.Validator,
                [typeof(Validation)] = TypeIds.Validation,
                [typeof(ValidationSummary)] = TypeIds.ValidationSummary,
            };
        }

#region Optimized metadata accessors
        public static bool IsInherited(DotvvmPropertyId propertyId)
        {
            if (propertyId.CanUseFastAccessors)
                return false;

            return BitmapRead(controls[propertyId.TypeId].inheritedBitmap, propertyId.MemberId);
        }

        public static bool UsesStandardAccessors(DotvvmPropertyId propertyId)
        {
            if (propertyId.CanUseFastAccessors)
            {
                return true;
            }
            else
            {
                var bitmap = controls[propertyId.TypeId].standardBitmap;
                var index = propertyId.MemberId;
                return BitmapRead(bitmap, index);
            }
        }

        public static bool IsActive(DotvvmPropertyId propertyId)
        {
            Debug.Assert(DotvvmPropertyIdAssignment.GetProperty(propertyId) != null, $"Property {propertyId} not registered.");
            ulong[] bitmap;
            uint index;
            if (propertyId.IsPropertyGroup)
            {
                bitmap = propertyGroupActiveBitmap;
                index = propertyId.GroupId;
            }
            else
            {
                bitmap = controls[propertyId.TypeId].activeBitmap;
                index = propertyId.MemberId;
            }
            return BitmapRead(bitmap, index);
        }

        public static DotvvmProperty? GetProperty(DotvvmPropertyId id)
        {
            if (id.IsPropertyGroup)
            {
                var groupIx = id.GroupId;
                if (groupIx >= propertyGroups.Length)
                    return null;
                var group = propertyGroups[groupIx];
                if (group is null)
                    return null;

                return group.GetDotvvmProperty(id.MemberId);
            }
            else
            {
                var typeId = id.TypeId;
                if (typeId >= controls.Length)
                    return null;
                var typeProps = controls[typeId].properties;
                if (typeProps is null)
                    return null;
                return typeProps[id.MemberId];
            }
        }

        public static Compilation.IControlAttributeDescriptor? GetPropertyOrPropertyGroup(DotvvmPropertyId id)
        {
            if (id.IsPropertyGroup)
            {
                var groupIx = id.GroupId;
                if (groupIx >= propertyGroups.Length)
                    return null;
                return propertyGroups[groupIx];
            }
            else
            {
                var typeId = id.TypeId;
                if (typeId >= controls.Length)
                    return null;
                var typeProps = controls[typeId].properties;
                if (typeProps is null)
                    return null;
                return typeProps[id.MemberId];
            }
        }

        public static object? GetValueRaw(DotvvmBindableObject obj, DotvvmPropertyId id, bool inherit = true)
        {
            if (id.CanUseFastAccessors)
            {
                if (obj.properties.TryGet(id, out var value))
                    return value;

                return propertyGroups[id.GroupId]!.DefaultValue;
            }
            else
            {
                var property = controls[id.TypeId].properties[id.MemberId];
                return property!.GetValue(obj, inherit);
            }
        }

        public static MarkupOptionsAttribute GetMarkupOptions(DotvvmPropertyId id)
        {
            if (id.IsPropertyGroup)
            {
                var groupIx = id.GroupId;
                return propertyGroups[groupIx]!.MarkupOptions;
            }
            else
            {
                var typeId = id.TypeId;
                var typeProps = controls[typeId].properties;
                return typeProps[id.MemberId]!.MarkupOptions;
            }
        }

        /// <summary> Property or property group has type assignable to IBinding and bindings should not be evaluated in GetValue </summary>
        public static bool IsBindingProperty(DotvvmPropertyId id)
        {
            if (id.IsPropertyGroup)
            {
                var groupIx = id.GroupId;
                return propertyGroups[groupIx]!.IsBindingProperty;
            }
            else
            {
                var typeId = id.TypeId;
                var typeProps = controls[typeId].properties;
                return typeProps[id.MemberId]!.IsBindingProperty;
            }
        }

        public static DotvvmPropertyGroup? GetPropertyGroup(ushort id)
        {
            if (id >= propertyGroups.Length)
                return null;
            return propertyGroups[id];
        }
#endregion

#region Registration
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort RegisterType(Type type)
        {
            if (typeIds.TryGetValue(type, out var existingId) && controls[existingId].locker is {})
                return existingId;

            return unlikely(type);

            static ushort unlikely(Type type)
            {
                var types = MemoryMarshal.CreateReadOnlySpan(ref type, 1);
                Span<ushort> ids = stackalloc ushort[1];
                RegisterTypes(types, ids);
                return ids[0];
            }
        }
        public static void RegisterTypes(ReadOnlySpan<Type> types, Span<ushort> ids)
        {
            if (types.Length == 0)
                return;

            lock (controlTypeRegisterLock)
            {
                if (controlCounter + types.Length >= controls.Length)
                {
                    VolatileResize(ref controls, 1 << (BitOperations.Log2((uint)(controlCounter + types.Length)) + 1));
                }
                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    if (!typeIds.TryGetValue(type, out var id))
                    {
                        id = (ushort)controlCounter++;
                    }
                    if (controls[id].locker is null)
                    {
                        controls[id].locker = new object();
                        controls[id].controlType = type;
                        controls[id].properties = new DotvvmProperty[DEFAULT_PROPERTY_COUNT];
                        controls[id].inheritedBitmap = new ulong[(DEFAULT_PROPERTY_COUNT - 1) / 64 + 1];
                        controls[id].standardBitmap = new ulong[(DEFAULT_PROPERTY_COUNT - 1) / 64 + 1];
                        controls[id].activeBitmap = new ulong[(DEFAULT_PROPERTY_COUNT - 1) / 64 + 1];
                        typeIds[type] = id;
                    }
                    ids[i] = id;
                }
            }
        }
        public static DotvvmPropertyId RegisterProperty(DotvvmProperty property)
        {
            if (property.GetType() == typeof(GroupedDotvvmProperty))
                throw new ArgumentException("RegisterProperty cannot be called with GroupedDotvvmProperty!");

            var typeCanUseDirectAccess = TypeCanUseAnyDirectAccess(property.GetType());
            var canUseDirectAccess = !property.IsValueInherited && typeCanUseDirectAccess;

            var typeId = RegisterType(property.DeclaringType);
            ref ControlTypeInfo control = ref controls[typeId];
            lock (control.locker) // single control registrations are sequential anyway
            {
                uint id;
                if (canUseDirectAccess)
                {
                    control.counterStandard += 1;
                    id = control.counterStandard * 2;
                }
                else
                {
                    control.counterNonStandard += 1;
                    id = control.counterNonStandard * 2 + 1;
                }
                if (id > ushort.MaxValue)
                    ThrowTooManyException(property);

                // resize arrays (we hold a write lock, but others may be reading in parallel)
                if (id >= control.properties.Length)
                {
                    VolatileResize(ref control.properties, control.properties.Length * 2);
                }
                if (id / 64 >= control.inheritedBitmap.Length)
                {
                    Debug.Assert(control.inheritedBitmap.Length == control.standardBitmap.Length);
                    Debug.Assert(control.inheritedBitmap.Length == control.activeBitmap.Length);

                    VolatileResize(ref control.inheritedBitmap, control.inheritedBitmap.Length * 2);
                    VolatileResize(ref control.standardBitmap, control.standardBitmap.Length * 2);
                    VolatileResize(ref control.activeBitmap, control.activeBitmap.Length * 2);
                }

                if (property.IsValueInherited)
                    BitmapSet(control.inheritedBitmap, (uint)id);
                if (typeCanUseDirectAccess)
                    BitmapSet(control.standardBitmap, (uint)id);
                if (property is ActiveDotvvmProperty)
                    BitmapSet(control.activeBitmap, (uint)id);

                control.properties[id] = property;
                return new DotvvmPropertyId(typeId, (ushort)id);
            }

            static void ThrowTooManyException(DotvvmProperty property) =>
                throw new Exception($"Too many properties registered for a single control type ({property.DeclaringType.ToCode()}). Trying to register property '{property.Name}: {property.PropertyType.ToCode()}'");
        }

        private static readonly ConcurrentDictionary<Type, (bool getter, bool setter)> cacheTypeCanUseDirectAccess = new(concurrencyLevel: 1, capacity: 10);
        /// <summary> Does the property use the default GetValue/SetValue methods? </summary>
        public static (bool getter, bool setter) TypeCanUseDirectAccess(Type propertyType)
        {
            if (propertyType == typeof(DotvvmProperty) || propertyType == typeof(GroupedDotvvmProperty))
                return (true, true);

            if (propertyType == typeof(DotvvmCapabilityProperty) || propertyType == typeof(DotvvmPropertyAlias) || propertyType == typeof(CompileTimeOnlyDotvvmProperty))
                return (false, false);

            if (propertyType.IsGenericType)
            {
                propertyType = propertyType.GetGenericTypeDefinition();
                if (propertyType == typeof(DelegateActionProperty<>))
                    return (true, true);
            }

            return cacheTypeCanUseDirectAccess.GetOrAdd(propertyType, static t =>
            {
                var getter = t.GetMethod(nameof(DotvvmProperty.GetValue), new [] { typeof(DotvvmBindableObject), typeof(bool) })!.DeclaringType == typeof(DotvvmProperty);
                var setter = t.GetMethod(nameof(DotvvmProperty.SetValue), new [] { typeof(DotvvmBindableObject), typeof(object) })!.DeclaringType == typeof(DotvvmProperty);
                return (getter, setter);
            });
        }
        public static bool TypeCanUseAnyDirectAccess(Type propertyType)
        {
            var (getter, setter) = TypeCanUseDirectAccess(propertyType);
            return getter && setter;
        }

        public static ushort RegisterPropertyGroup(DotvvmPropertyGroup group)
        {
            lock (groupRegisterLock)
            {
                var id = (ushort)groupCounter++;
                if (id == 0)
                    throw new Exception("Too many property groups registered already.");

                if (id >= propertyGroups.Length)
                {
                    VolatileResize(ref propertyGroups, propertyGroups.Length * 2);
                    VolatileResize(ref propertyGroupActiveBitmap, propertyGroupActiveBitmap.Length * 2);
                }

                propertyGroups[id] = group;
                if (group is ActiveDotvvmPropertyGroup)
                    BitmapSet(propertyGroupActiveBitmap, id);
                return id;
            }
        }

        /// <summary> Thread-safe to read from the array while we are resizing </summary>
        private static void VolatileResize<T>(ref T[] array, int newSize)
        {
            var localRef = array;
            var newArray = new T[newSize];
            localRef.AsSpan().CopyTo(newArray.AsSpan(0, localRef.Length));
            Volatile.Write(ref array, newArray);
        }

#endregion Registration

#region Group members
        private static ushort PredefinedPropertyGroupMemberId(ReadOnlySpan<char> name)
        {
            switch (name)
            {
                case "class": return GroupMembers.@class;
                case "id": return GroupMembers.id;
                case "style": return GroupMembers.style;
                case "name": return GroupMembers.name;
                case "data-bind": return GroupMembers.databind;
                default: return 0;
            }
        }

        public static ushort GetGroupMemberId(string name, bool registerIfNotFound)
        {
            var id = PredefinedPropertyGroupMemberId(name);
            if (id != 0)
                return id;
            if (propertyGroupMemberIds.TryGetValue(name, out id))
                return id;
            if (!registerIfNotFound)
                return 0;
            return RegisterGroupMember(name);
        }

        private static ushort RegisterGroupMember(string name)
        {
            lock (groupMemberRegisterLock)
            {
                if (propertyGroupMemberIds.TryGetValue(name, out var id))
                    return id;
                id = (ushort)(propertyGroupMemberIds.Count + 1);
                if (id == 0)
                    throw new Exception("Too many property group members registered already.");
                if (id >= propertyGroupMemberNames.Length)
                    VolatileResize(ref propertyGroupMemberNames, propertyGroupMemberNames.Length * 2);
                propertyGroupMemberNames[id] = name;
                propertyGroupMemberIds[name] = id;
                return id;
            }
        }

        internal static string? GetGroupMemberName(ushort id)
        {
            if (id < propertyGroupMemberNames.Length)
                return propertyGroupMemberNames[id];
            return null;
        }
#endregion Group members

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool BitmapRead(ulong[] bitmap, uint index)
        {
            return (bitmap[index / 64] & (1ul << (int)(index % 64))) != 0;
        }

        static void BitmapSet(ulong[] bitmap, uint index)
        {
            bitmap[index / 64] |= 1ul << (int)(index % 64);
        }

        private struct ControlTypeInfo
        {
            public DotvvmProperty?[] properties;
            public ulong[] inheritedBitmap;
            public ulong[] standardBitmap;
            public ulong[] activeBitmap;
            public object locker;
            public Type controlType;
            public uint counterStandard;
            public uint counterNonStandard;
        }

        public static class GroupMembers
        {
            public const ushort id = 1;
            public const ushort @class = 2;
            public const ushort style = 3;
            public const ushort name = 4;
            public const ushort databind = 5;
        }

        public static class TypeIds
        {
            public const ushort DotvvmBindableObject = 1;
            public const ushort DotvvmControl = 2;
            public const ushort HtmlGenericControl = 3;
            public const ushort RawLiteral = 4;
            public const ushort Literal = 5;
            public const ushort ButtonBase = 6;
            public const ushort Button = 7;
            public const ushort LinkButton = 8;
            public const ushort TextBox = 9;
            public const ushort RouteLink = 10;
            public const ushort CheckableControlBase = 11;
            public const ushort CheckBox = 12;
            public const ushort Validator = 13;
            public const ushort Validation = 14;
            public const ushort ValidationSummary = 15;
            // public const short Internal = 4;
        }

        public static class PropertyIds
        {
            public const uint DotvvmBindableObject_DataContext = TypeIds.DotvvmBindableObject << 16 | 1;
        }
    }
}
