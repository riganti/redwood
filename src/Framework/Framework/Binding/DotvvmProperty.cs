using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;
using System.Diagnostics;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using DotVVM.Framework.Runtime;
using System.Threading;
using System.Text.Json.Serialization;

namespace DotVVM.Framework.Binding
{
    /// <summary>
    /// Represents a property of DotVVM controls.
    /// </summary>
    [DebuggerDisplay("{FullName}")]
    public class DotvvmProperty : IPropertyDescriptor
    {
        public DotvvmPropertyId Id { get; }

        /// <summary>
        /// Gets or sets the name of the property.
        /// </summary>
        public string Name { get; }


        [JsonIgnore]
        ITypeDescriptor IControlAttributeDescriptor.DeclaringType => new ResolvedTypeDescriptor(DeclaringType);

        [JsonIgnore]
        ITypeDescriptor IControlAttributeDescriptor.PropertyType => new ResolvedTypeDescriptor(PropertyType);

        /// <summary>
        /// Gets the default value of the property.
        /// </summary>
        public object? DefaultValue { get; protected set; }

        /// <summary>
        /// Gets the type of the property.
        /// </summary>
        public Type PropertyType { get; protected set; }

        /// <summary>
        /// Gets the type of the class where the property is registered.
        /// </summary>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets whether the value can be inherited from the parent controls.
        /// </summary>
        public bool IsValueInherited { get; protected set; }

        /// <summary>
        /// Gets or sets the Reflection property information.
        /// </summary>
        [JsonIgnore]
        public PropertyInfo? PropertyInfo { get; protected set; }

        /// <summary>
        /// Provider of custom attributes for this property.
        /// </summary>
        internal ICustomAttributeProvider AttributeProvider { get; private protected set; }

        /// <summary>
        /// Gets or sets the markup options.
        /// </summary>
        public MarkupOptionsAttribute MarkupOptions { get; protected set; }

        /// <summary>
        /// Determines if property type inherits from IBinding
        /// </summary>
        public bool IsBindingProperty { get; protected set; }

        /// <summary>
        /// Gets the full name of the descriptor.
        /// </summary>
        public string DescriptorFullName
        {
            get { return DeclaringType.FullName + "." + Name + "Property"; }
        }

        /// <summary>
        /// Gets the full name of the property.
        /// </summary>
        public string FullName
        {
            get { return DeclaringType.Name + "." + Name; }
        }

        [JsonIgnore]
        public DataContextChangeAttribute[] DataContextChangeAttributes { get; protected set; }

        [JsonIgnore]
        public DataContextStackManipulationAttribute? DataContextManipulationAttribute { get; protected set; }

        public ObsoleteAttribute? ObsoleteAttribute { get; protected set; }

        /// <summary> The capability which declared this property. When the property is declared by an capability, it can only be used by this capability. </summary>
        public DotvvmCapabilityProperty? OwningCapability { get; internal set; }
        /// <summary> The capabilities which use this property. </summary>
        public ImmutableArray<DotvvmCapabilityProperty> UsedInCapabilities { get; internal set; } = ImmutableArray<DotvvmCapabilityProperty>.Empty;
        IPropertyDescriptor? IControlAttributeDescriptor.OwningCapability => OwningCapability;
        IEnumerable<IPropertyDescriptor> IControlAttributeDescriptor.UsedInCapabilities => UsedInCapabilities;

        private bool initialized = false;


        internal void AddUsedInCapability(DotvvmCapabilityProperty? p)
        {
            if (p is object)
                lock(this)
                {
                    if (UsedInCapabilities.Contains(p)) return;

                    var newArray = UsedInCapabilities.Add(p);
                    Thread.MemoryBarrier(); // make sure the array is complete before we let other threads use it lock-free
                    UsedInCapabilities = newArray;
                }
        }

#pragma warning disable CS8618 // DotvvmProperty is usually initialized by InitializeProperty
        internal DotvvmProperty(string name, Type declaringType, bool isValueInherited)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (declaringType is null) throw new ArgumentNullException(nameof(declaringType));
            this.Name = name;
            this.DeclaringType = declaringType;
            this.IsValueInherited = isValueInherited;
            this.Id = DotvvmPropertyIdAssignment.RegisterProperty(this);
        }
        internal DotvvmProperty(DotvvmPropertyId id, string name, Type declaringType)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (declaringType is null) throw new ArgumentNullException(nameof(declaringType));
            if (id.Id == 0) throw new ArgumentException("DotvvmProperty must have an ID", nameof(id));
            this.Name = name;
            this.DeclaringType = declaringType;
            this.Id = id;
        }
        internal DotvvmProperty(
#pragma warning restore CS8618
            string name,
            Type type,
            Type declaringType,
            object? defaultValue,
            bool isValueInherited,
            ICustomAttributeProvider attributeProvider,
            DotvvmPropertyId id = default
        )
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.PropertyType = type ?? throw new ArgumentNullException(nameof(type));
            this.DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            this.DefaultValue = defaultValue;
            this.IsValueInherited = isValueInherited;
            this.AttributeProvider = attributeProvider ?? throw new ArgumentNullException(nameof(attributeProvider));
            if (id.Id == 0)
                id = DotvvmPropertyIdAssignment.RegisterProperty(this);
            this.Id = id;
            InitializeProperty(this);
        }

        public T[] GetAttributes<T>()
        {
            if (PropertyInfo == null)
                return AttributeProvider.GetCustomAttributes<T>();
            if (object.ReferenceEquals(AttributeProvider, PropertyInfo))
                return PropertyInfo.GetCustomAttributes<T>();
            var attrA = AttributeProvider.GetCustomAttributes<T>();
            var attrB = PropertyInfo.GetCustomAttributes<T>();
            if (attrA.Length == 0) return attrB;
            if (attrB.Length == 0) return attrA;
            return attrA.Concat(attrB).ToArray();
        }

        public T? GetAttribute<T>() where T: Attribute
        {
            var t = typeof(T);
            var provider = AttributeProvider;
            if (provider.IsDefined(t, true))
            {
                return (T)provider.GetCustomAttributes(t, true).Single();
            }
            var property = PropertyInfo;
            if (property is {} && !object.ReferenceEquals(property, provider))
            {
                return (T?)property.GetCustomAttribute(t, true);
            }

            return null;
        }

        public bool IsOwnedByCapability(Type capability) =>
            (this is DotvvmCapabilityProperty && this.PropertyType == capability) ||
            OwningCapability?.IsOwnedByCapability(capability) == true;

        public bool IsOwnedByCapability(DotvvmCapabilityProperty capability) =>
            this == capability ||
            OwningCapability?.IsOwnedByCapability(capability) == true;

        private object? GetInheritedValue(DotvvmBindableObject control)
        {
            for (var p = control.Parent; p is not null; p = p.Parent)
            {
                if (p.properties.TryGet(Id, out var v))
                    return v;
            }
            return DefaultValue;
        }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        public virtual object? GetValue(DotvvmBindableObject control, bool inherit = true)
        {
            if (control.properties.TryGet(Id, out var value))
            {
                return value;
            }
            if (IsValueInherited & inherit)
            {
                return GetInheritedValue(control);
            }
            return DefaultValue;
        }

        private bool IsSetInHierarchy(DotvvmBindableObject control)
        {
            for (var p = control.Parent; p is not null; p = p.Parent)
            {
                if (p.properties.Contains(Id))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets whether the value of the property is set
        /// </summary>
        public virtual bool IsSet(DotvvmBindableObject control, bool inherit = true)
        {
            if (control.properties.Contains(Id))
            {
                return true;
            }

            if (IsValueInherited && inherit)
            {
                return IsSetInHierarchy(control);
            }

            return false;
        }


        /// <summary>
        /// Sets the value of the property.
        /// </summary>
        public virtual void SetValue(DotvvmBindableObject control, object? value)
        {
            control.properties.Set(Id, value);
        }

        /// <summary>
        /// Registers the specified DotVVM property.
        /// </summary>
        public static DotvvmProperty Register<TPropertyType, TDeclaringType>(Expression<Func<DotvvmProperty?>> fieldAccessor, [AllowNull] TPropertyType defaultValue = default(TPropertyType), bool isValueInherited = false)
        {
            var field = ReflectionUtils.GetMemberFromExpression(fieldAccessor) as FieldInfo;
            if (field == null || !field.IsStatic) throw new ArgumentException("The expression should be simple static field access", nameof(fieldAccessor));
            if (!field.Name.EndsWith("Property", StringComparison.Ordinal)) throw new ArgumentException($"DotVVM property backing field's '{field.Name}' name should end with 'Property'");
            return Register<TPropertyType, TDeclaringType>(field.Name.Remove(field.Name.Length - "Property".Length).DotvvmInternString(trySystemIntern: true), defaultValue, isValueInherited);
        }

        /// <summary>
        /// Registers the specified DotVVM property.
        /// </summary>
        public static DotvvmProperty Register<TPropertyType, TDeclaringType>(Expression<Func<TDeclaringType, object?>> propertyAccessor, [AllowNull] TPropertyType defaultValue = default(TPropertyType), bool isValueInherited = false)
        {
            var property = ReflectionUtils.GetMemberFromExpression(propertyAccessor) as PropertyInfo;
            if (property == null) throw new ArgumentException("The expression should be simple property access", nameof(propertyAccessor));
            return Register<TPropertyType, TDeclaringType>(property.Name.DotvvmInternString(trySystemIntern: true), defaultValue, isValueInherited);
        }

        /// <summary>
        /// Registers the specified DotVVM property.
        /// </summary>
        public static DotvvmProperty Register<TPropertyType, TDeclaringType>(string propertyName, [AllowNull] TPropertyType defaultValue = default(TPropertyType), bool isValueInherited = false, DotvvmProperty? property = null)
        {
            var field = typeof(TDeclaringType).GetField(propertyName + "Property", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new ArgumentException($"'{typeof(TDeclaringType).Name}' does not contain static field '{propertyName}Property'.");

            return Register(propertyName, typeof(TPropertyType), typeof(TDeclaringType), BoxingUtils.BoxGeneric(defaultValue), isValueInherited, property, field);
        }

        public static DotvvmProperty Register(string propertyName, Type propertyType, Type declaringType, object? defaultValue, bool isValueInherited, DotvvmProperty? property, ICustomAttributeProvider attributeProvider, bool throwOnDuplicateRegistration = true)
        {
            if (propertyName is null) throw new ArgumentNullException(nameof(propertyName));
            if (propertyType is null) throw new ArgumentNullException(nameof(propertyType));
            if (declaringType is null) throw new ArgumentNullException(nameof(declaringType));
            if (attributeProvider is null) throw new ArgumentNullException(nameof(attributeProvider));

            if (property == null)
            {
                property = new DotvvmProperty(propertyName, propertyType, declaringType, defaultValue, isValueInherited, attributeProvider);
            }
            else
            {
                if (!property.initialized)
                {
                    property.PropertyType = propertyType;
                    property.DefaultValue = defaultValue;
                    property.IsValueInherited = isValueInherited;
                    property.AttributeProvider = attributeProvider;
                    InitializeProperty(property, attributeProvider);
                }
                if (property.Name != propertyName) throw new ArgumentException("The property name does not match the existing property.", nameof(propertyName));
                if (property.IsValueInherited != isValueInherited) throw new ArgumentException("The IsValueInherited does not match the existing property.", nameof(isValueInherited));
                if (property.DeclaringType != declaringType) throw new ArgumentException("The declaring type does not match the existing property.", nameof(declaringType));
                if (property.PropertyType != propertyType) throw new ArgumentException("The property type does not match the existing property.", nameof(propertyType));
                if (property.DefaultValue != defaultValue) throw new ArgumentException("The default value does not match the existing property.", nameof(defaultValue));
                if (property.AttributeProvider != attributeProvider) throw new ArgumentException("The attribute provider does not match the existing property.", nameof(attributeProvider));
            }

            return Register(property, throwOnDuplicateRegistration);
        }

        public record PropertyAlreadyExistsException(
            DotvvmProperty OldProperty,
            DotvvmProperty NewProperty
        )
            : DotvvmExceptionBase(RelatedProperty: OldProperty)
        {
            public override string Message { get {
                var capabilityHelp = OldProperty.OwningCapability is {} ownerOld ? $" The existing property is declared by capability {ownerOld.Name}." : "";
                var capabilityHelpNew = NewProperty.OwningCapability is {} ownerNew ? $" The new property is declared by capability {ownerNew.Name}." : "";
                var message = NewProperty is DotvvmCapabilityProperty ?
                              $"Capability {NewProperty.Name} conflicts with existing property. Consider giving the capability a different name." :
                              $"DotVVM property is already registered: {NewProperty.FullName}";
                return message + capabilityHelp + capabilityHelpNew;
            } }
        }

        internal static DotvvmProperty Register(DotvvmProperty property, bool throwOnDuplicateRegistration = true)
        {
            if (property.Id.Id == 0)
                throw new Exception("DotvvmProperty must have an ID");

            if (!property.initialized)
                throw new Exception("DotvvmProperty must be initialized before registration.");

            var key = (property.DeclaringType, property.Name);
            if (!registeredProperties.TryAdd(key, property))
            {
                if (throwOnDuplicateRegistration)
                    throw new PropertyAlreadyExistsException(registeredProperties[key], property);
                else
                    property = registeredProperties[key];
            }

            return property;
        }

        /// <summary>
        /// Registers an alias with a property accessor for another DotvvmProperty given by the PropertyAlias attribute.
        /// </summary>
        public static DotvvmPropertyAlias RegisterAlias<TDeclaringType>(
            Expression<Func<TDeclaringType, object?>> propertyAccessor)
        {
            var property = ReflectionUtils.GetMemberFromExpression(propertyAccessor.Body) as PropertyInfo;
            if (property == null)
            {
                throw new ArgumentException("The expression should be simple property access",
                    nameof(propertyAccessor));
            }
            return RegisterAlias<TDeclaringType>(property.Name);
        }

        /// <summary>
        /// Registers an alias for another DotvvmProperty given by the PropertyAlias attribute.
        /// </summary>
        public static DotvvmPropertyAlias RegisterAlias<TDeclaringType>(
            Expression<Func<DotvvmProperty?>> fieldAccessor)
        {
            var field = ReflectionUtils.GetMemberFromExpression(fieldAccessor) as FieldInfo;
            if (field == null || !field.IsStatic)
            {
                throw new ArgumentException("The expression should be simple static field access",
                    nameof(fieldAccessor));
            }
            if (!field.Name.EndsWith("Property", StringComparison.Ordinal))
            {
                throw new ArgumentException($"DotVVM property backing field's '{field.Name}' "
                    + "name should end with 'Property'");
            }
            return RegisterAlias<TDeclaringType>(field.Name.Remove(field.Name.Length - "Property".Length));
        }

        /// <summary>
        /// Registers an alias for another DotvvmProperty given by the PropertyAlias attribute.
        /// </summary>
        public static DotvvmPropertyAlias RegisterAlias<TDeclaringType>(string aliasName)
        {
            var field = typeof(TDeclaringType).GetField(
                aliasName + "Property",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new ArgumentException($"'{typeof(TDeclaringType).Name}' does not contain static field '{aliasName}Property'.");
            }
            return RegisterAlias(aliasName, typeof(TDeclaringType), field);
        }

        /// <summary>
        /// Registers an alias for a DotvvmProperty.
        /// </summary>
        public static DotvvmPropertyAlias RegisterAlias(
            string aliasName,
            Type declaringType,
            ICustomAttributeProvider attributeProvider,
            bool throwOnDuplicitRegistration = true)
        {
            var propertyInfo = declaringType.GetProperty(aliasName);
            attributeProvider = propertyInfo ?? attributeProvider;
            var aliasAttribute = attributeProvider.GetCustomAttribute<PropertyAliasAttribute>();
            if (aliasAttribute is null)
            {
                throw new ArgumentException($"A property alias must have a {nameof(PropertyAliasAttribute)}.");
            }

            var propertyAlias = new DotvvmPropertyAlias(
                aliasName,
                declaringType,
                aliasAttribute.AliasedPropertyName,
                aliasAttribute.AliasedPropertyDeclaringType ?? declaringType,
                attributeProvider);
            propertyAlias.ObsoleteAttribute = attributeProvider.GetCustomAttribute<ObsoleteAttribute>();
            var key = (propertyAlias.DeclaringType, propertyAlias.Name);

            if (!registeredProperties.TryAdd(key, propertyAlias))
            {
                if (throwOnDuplicitRegistration)
                    throw new PropertyAlreadyExistsException(registeredProperties[key], propertyAlias);
            }

            if (!registeredAliases.TryAdd(key, propertyAlias))
            {
                if (throwOnDuplicitRegistration)
                    throw new ArgumentException($"Property alias is already registered: {propertyAlias.FullName}");
                else
                    return registeredAliases[key];
            }
            return propertyAlias;
        }

        public static void InitializeProperty(DotvvmProperty property, ICustomAttributeProvider? attributeProvider = null)
        {
            if (property.initialized)
                throw new Exception("DotvvmProperty should not be initialized twice.");
            if (string.IsNullOrWhiteSpace(property.Name))
                throw new Exception("DotvvmProperty must not have empty name.");
            if (property.DeclaringType is null || property.PropertyType is null)
                throw new Exception($"DotvvmProperty {property.DeclaringType?.Name}.{property.Name} must have PropertyType and DeclaringType.");

            Interlocked.Increment(ref Hosting.DotvvmMetrics.BareCounters.DotvvmPropertyInitialized);

            property.PropertyInfo ??= property.DeclaringType.GetProperty(property.Name);
            property.AttributeProvider ??=
                attributeProvider ??
                property.PropertyInfo ??
                throw new ArgumentNullException(nameof(attributeProvider));
            property.MarkupOptions ??=
                property.GetAttribute<MarkupOptionsAttribute>()
                ?? new MarkupOptionsAttribute();
            if (string.IsNullOrEmpty(property.MarkupOptions.Name))
                property.MarkupOptions.Name = property.Name;

            property.DataContextChangeAttributes ??=
                property.GetAttributes<DataContextChangeAttribute>();
            property.DataContextManipulationAttribute ??=
                property.GetAttribute<DataContextStackManipulationAttribute>();
            if (property.DataContextManipulationAttribute != null && property.DataContextChangeAttributes.Length != 0)
                throw new ArgumentException($"{nameof(DataContextChangeAttributes)} and {nameof(DataContextManipulationAttribute)} cannot be set both at property '{property.FullName}'.");
            property.IsBindingProperty = typeof(IBinding).IsAssignableFrom(property.PropertyType);
            property.ObsoleteAttribute = property.GetAttribute<ObsoleteAttribute>();

            if (property.IsBindingProperty)
            {
                property.MarkupOptions.AllowHardCodedValue = false;
            }

            property.initialized = true;
        }

        public static void CheckAllPropertiesAreRegistered(Type controlType)
        {
            var properties =
               (from p in controlType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                where !registeredProperties.ContainsKey((p.DeclaringType!, p.Name))
                where !p.IsDefined(typeof(PropertyGroupAttribute))
                let markupOptions = p.GetCustomAttribute<MarkupOptionsAttribute>()
                where markupOptions != null && markupOptions.MappingMode != MappingMode.Exclude
                select p).ToArray();

            if (properties.Any())
            {
                var controlHasOtherProperties = registeredProperties.Values.Any(p => p.DeclaringType == controlType);
                var explicitLoadingHelp =
                    !controlHasOtherProperties ? $" The control does not have any other properties registered. It could indicate that DotVVM did not register properties from this assembly - it is common with ExplicitAssemblyLoading enabled, try to register {controlType.Assembly.GetName().Name} into config.Markup.Assemblies." : "";
                var deprecationHelp = " DotVVM version <= 3.x did support this, but this feature was removed as it lead to many issues. Please register the property using DotvvmProperty.Register. If you find this annoyingly verbose, you could use control capabilities instead (using DotvvmCapabilityProperty.Register).";
                throw new NotSupportedException($"Control '{controlType.Name}' has properties that are not registered as a DotvvmProperty but have a MarkupOptionsAttribute: {string.Join(", ", properties.Select(p => p.Name))}." + explicitLoadingHelp + deprecationHelp);
            }
        }

        // TODO: figure out how to refresh on hot reload
        private static readonly ConcurrentDictionary<(Type, string), DotvvmProperty> registeredProperties = new();
        private static readonly ConcurrentDictionary<(Type, string), DotvvmPropertyAlias> registeredAliases = new();

        /// <summary>
        /// Resolves the <see cref="DotvvmProperty"/> by the declaring type and name.
        /// </summary>
        public static DotvvmProperty? ResolveProperty(Type type, string name)
        {
            DotvvmProperty? property;
            while (!registeredProperties.TryGetValue((type, name), out property) && type.BaseType != null)
            {
                type = type.BaseType;
            }
            return property;
        }

        /// <summary>
        /// Resolves the <see cref="DotvvmProperty"/> from the full name (DeclaringTypeName.PropertyName).
        /// </summary>
        public static DotvvmProperty? ResolveProperty(string fullName, bool caseSensitive = true)
        {
            return registeredProperties.Values.LastOrDefault(p =>
                p.FullName.Equals(fullName, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves all properties of specified type.
        /// </summary>
        public static DotvvmProperty[] ResolveProperties(Type type)
        {
            var types = new HashSet<Type>();
            while (type.BaseType != null)
            {
                types.Add(type);
                type = type.BaseType;
            }

            return registeredProperties.Values.Where(p => types.Contains(p.DeclaringType)).ToArray();
        }

        public static IEnumerable<DotvvmProperty> AllProperties => registeredProperties.Values;
        public static IEnumerable<DotvvmPropertyAlias> AllAliases => registeredAliases.Values;

        public override string ToString()
        {
            return $"{this.FullName}";
        }
    }
}
