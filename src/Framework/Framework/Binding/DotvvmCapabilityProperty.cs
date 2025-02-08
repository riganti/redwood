using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Utils;
using FastExpressionCompiler;

namespace DotVVM.Framework.Binding
{
    /// <summary> Descriptor of a DotVVM capability.
    /// Capability is a way to register multiple properties at once in DotVVM. </summary>
    public partial class DotvvmCapabilityProperty : DotvvmProperty
    {
        internal Func<DotvvmBindableObject, object> Getter { get; private set; } = null!;
        internal Action<DotvvmBindableObject, object> Setter { get; private set; } = null!;
        internal Func<ResolvedControl, ResolvedPropertyCapability>? ResolvedControlGetter { get; private set; } = null;
        internal Action<ResolvedControl, ResolvedPropertyCapability>? ResolvedControlSetter { get; private set; } = null;


        /// <summary> List of properties that this capability contains. Note that this may contain nested capabilities. </summary>
        public ImmutableArray<(PropertyInfo prop, DotvvmProperty dotvvmProperty)>? PropertyMapping { get; private set; }
        /// <summary> List of property groups that this capability contains. Note that other property groups may be in nested capabilities (see the <see cref="PropertyMapping" /> array). </summary>
        public ImmutableArray<(PropertyInfo prop, DotvvmPropertyGroup dotvvmPropertyGroup)>? PropertyGroupMapping { get; private set; }
        /// <summary> Prefix prepended to all properties registered by this capability. </summary>
        public string Prefix { get; }

        private static ConcurrentDictionary<(Type declaringType, Type capabilityType, string prefix), DotvvmCapabilityProperty> capabilityRegistry = new();
        private static ConcurrentDictionary<(Type declaringType, Type capabilityType), ImmutableArray<DotvvmCapabilityProperty>> capabilityListRegistry = new();

        private DotvvmCapabilityProperty(
            string prefix,
            string? name,
            Type type,
            Type declaringType,
            ICustomAttributeProvider? attributeProvider,
            DotvvmCapabilityProperty? declaringCapability
        ): base(name ?? prefix + type.Name, declaringType, isValueInherited: false)
        {
            this.PropertyType = type;
            this.Prefix = prefix;
            this.AddUsedInCapability(declaringCapability);

            if (!type.IsDefined(typeof(DotvvmControlCapabilityAttribute)))
                throw new InvalidCapabilityTypeException(this, "is missing the [DotvvmControlCapability] attribute");

            if (!type.IsSealed)
                throw new InvalidCapabilityTypeException(this, $"is not sealed. Capability should be a sealed record with {{ init; get; }} properties (also may be a sealed class or a struct)");

            AssertPropertyNotDefined(this, postContent: false);

            var dotnetFieldName = ToPascalCase(Name.Replace("-", "_").Replace(":", "_"));
            attributeProvider ??=
                declaringType.GetProperty(dotnetFieldName) ??
                declaringType.GetField(dotnetFieldName) ??
                (ICustomAttributeProvider?)declaringType.GetField(dotnetFieldName + "Property") ??
                throw new Exception($"Capability backing field could not be found and capabilityAttributeProvider argument was not provided. Property: {declaringType.Name}.{Name}. Please declare a field or property named {dotnetFieldName}.");

            DotvvmProperty.InitializeProperty(this, attributeProvider);
            this.MarkupOptions._mappingMode ??= MappingMode.Exclude;
        }

        public override object GetValue(DotvvmBindableObject control, bool inherit = true) => Getter(control);

        public override void SetValue(DotvvmBindableObject control, object? value)
        {
            _ = value ?? throw new DotvvmControlException($"Capability can not be set to null") { RelatedProperty = this };
            Setter(control, value);
        }
        
        /// <summary> Looks up a capability on the specified control (<paramref name="declaringType"/>).
        /// If multiple capabilities of this type are registered, <see cref="Find(Type, Type, string)" /> method must be used to retrieve the one with specified prefix. </summary>
        public static DotvvmCapabilityProperty? Find(Type? declaringType, Type capabilityType)
        {
            var c = GetCapabilities(declaringType, capabilityType);
            if (c.Length == 1) return c[0];
            else return null;
        }

        /// <summary> Looks up a capability on the specified control (<paramref name="declaringType"/>). </summary>
        public static DotvvmCapabilityProperty? Find(Type? declaringType, Type capabilityType, string? globalPrefix)
        {
            if (globalPrefix is null)
                return Find(declaringType, capabilityType);
            while (declaringType != typeof(DotvvmBindableObject) && declaringType is not null)
            {
                if (capabilityRegistry.TryGetValue((declaringType, capabilityType, globalPrefix), out var result))
                    return result;
                declaringType = declaringType.BaseType;
            }
            return null;
        }

        /// <summary> Lists capabilities on the specified control (<paramref name="declaringType"/>). </summary>
        public static IEnumerable<DotvvmCapabilityProperty> GetCapabilities(Type declaringType) =>
            capabilityRegistry.Values.Where(c => c.DeclaringType.IsAssignableFrom(declaringType));

        /// <summary> Lists capabilities of the selected type on the specified control (<paramref name="declaringType"/>). </summary>
        public static ImmutableArray<DotvvmCapabilityProperty> GetCapabilities(Type? declaringType, Type capabilityType)
        {
            var r = ImmutableArray<DotvvmCapabilityProperty>.Empty;
            while (declaringType != typeof(DotvvmBindableObject) && declaringType is not null)
            {
                if (capabilityListRegistry.TryGetValue((declaringType, capabilityType), out var rr))
                {
                    r = r.AddRange(rr);
                }
                declaringType = declaringType.BaseType;
            }
            return r;
        }

        /// <summary> Returns an iterator of the <see cref="DotvvmProperty.OwningCapability" /> chain. The first element is this capability. </summary>
        public IEnumerable<DotvvmCapabilityProperty> ThisAndOwners()
        {
            for (var x = this; x is object; x = x.OwningCapability)
                yield return x;
        }

        /// <summary> Return a <see cref="DotvvmProperty" /> defined by this capability with the specified name. `null` is returned if there is no property with the name or if this capability does not have property mapping.
        /// <code> HtmlGenericControl.HtmlCapabilityProperty.FindProperty("Visible") </code> </summary>
        public DotvvmProperty? FindProperty(string name) =>
            PropertyMapping?.FirstOrDefault(p => p.prop.Name == name).dotvvmProperty;

        /// <summary> Return a <see cref="DotvvmPropertyGroup" /> defined by this capability with the specified name. `null` is returned if there is no property with the name or if this capability does not have property mapping.
        /// <code> HtmlGenericControl.HtmlCapabilityProperty.FindPropertyGroup("Attributes") </code> </summary>
        public DotvvmPropertyGroup? FindPropertyGroup(string name) =>
            PropertyGroupMapping?.FirstOrDefault(p => p.prop.Name == name).dotvvmPropertyGroup;

        private static void AssertPropertyNotDefined(DotvvmCapabilityProperty p, bool postContent = false)
        {
            if (Find(p.DeclaringType.NotNull(), p.PropertyType, p.Prefix) is {} existingCapability)
                throw new CapabilityAlreadyExistsException(existingCapability, postContent);
            if (DotvvmProperty.ResolveProperty(p.DeclaringType, p.Name) is DotvvmProperty existingProp)
                throw new PropertyAlreadyExistsException(existingProp, p);
        }

        /// <summary> Registers a new DotVVM capability. For a given <typeparamref name="TCapabilityType"/>, this method will register a DotVVM property for each property of the capability type. </summary>
        /// <param name="globalPrefix"> Prefix prepended to all properties registered by this capability. </param>
        /// <param name="name"> Name of the DotvvmProperty which will contain the capability. If not specified, name of <typeparamref name="TCapabilityType"/> will be used. </param>
        /// <param name="capabilityAttributeProvider"> A member info from System.Reflection which will be used to look for attributes. If not specified, DotVVM will look for property or field with the specified <paramref name="name"/>. </param>
        public static DotvvmCapabilityProperty RegisterCapability<TCapabilityType, TDeclaringType>(string globalPrefix = "", string? name = null, ICustomAttributeProvider? capabilityAttributeProvider = null) =>
            RegisterCapability(typeof(TDeclaringType), typeof(TCapabilityType), globalPrefix, name, capabilityAttributeProvider);
        /// <summary> Registers a new DotVVM capability. For a given <paramref name="capabilityType"/>, this method will register a DotVVM property for each property of the capability type. </summary>
        /// <param name="globalPrefix"> Prefix prepended to all properties registered by this capability. </param>
        /// <param name="name"> Name of the DotvvmProperty which will contain the capability. If not specified, name of <paramref name="capabilityType"/> will be used. </param>
        /// <param name="capabilityAttributeProvider"> A member info from System.Reflection which will be used to look for attributes. If not specified, DotVVM will look for property or field with the specified <paramref name="name"/>. </param>
        public static DotvvmCapabilityProperty RegisterCapability(Type declaringType, Type capabilityType, string globalPrefix = "", string? name = null, ICustomAttributeProvider? capabilityAttributeProvider = null, DotvvmCapabilityProperty? declaringCapability = null)
        {
            var prop = new DotvvmCapabilityProperty(
                globalPrefix,
                name,
                capabilityType,
                declaringType,
                capabilityAttributeProvider!,
                declaringCapability
            ) { 
                OwningCapability = declaringCapability
            };
            InitializeCapability(prop);

            AssertPropertyNotDefined(prop, postContent: true);

            var valueParameter = Expression.Parameter(typeof(object), "value");

            return RegisterCapability(prop);
        }

        public static DotvvmCapabilityProperty RegisterCapability<TCapabilityType, TDeclaringType>(
            Func<TDeclaringType, TCapabilityType> getter,
            Action<TDeclaringType, TCapabilityType> setter,
            string prefix = "",
            string? name = null,
            ICustomAttributeProvider? capabilityAttributeProvider = null)
            where TCapabilityType : notnull
            where TDeclaringType : DotvvmBindableObject =>
            RegisterCapability(typeof(TDeclaringType), typeof(TCapabilityType), (o) => (object)getter((TDeclaringType)o), (o, x) => setter((TDeclaringType)o, (TCapabilityType)x!), prefix, name);
        public static DotvvmCapabilityProperty RegisterCapability(Type declaringType, Type capabilityType, Func<DotvvmBindableObject, object> getter, Action<DotvvmBindableObject, object?> setter, string prefix = "", string? name = null) =>
            RegisterCapability(
                new DotvvmCapabilityProperty(prefix, name, capabilityType, declaringType, null, null) {
                    Getter = getter,
                    Setter = setter,
                }
            );

        static DotvvmCapabilityProperty RegisterCapability(DotvvmCapabilityProperty property)
        {
            var declaringType = property.DeclaringType.NotNull();
            var capabilityType = property.PropertyType.NotNull();
            AssertPropertyNotDefined(property);
            DotvvmProperty.Register(property);
            if (!capabilityRegistry.TryAdd((declaringType, capabilityType, property.Prefix), property))
                throw new($"unhandled naming conflict when registering capability {capabilityType}.");
            capabilityListRegistry.AddOrUpdate(
                (declaringType, capabilityType),
                ImmutableArray.Create(property),
                (_, old) => old.Add(property));
            return property;
        }

        static void InitializeCapability(DotvvmCapabilityProperty resultProperty)
        {
            var declaringType = resultProperty.DeclaringType.NotNull();
            var capabilityType = resultProperty.PropertyType.NotNull();
            var globalPrefix = resultProperty.Prefix;
            var properties = capabilityType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            if (properties.Length == 0)
                throw new InvalidCapabilityTypeException(resultProperty, "does not have any properties");

            if (capabilityType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidCapabilityTypeException(resultProperty, "does not have a parameterless constructor");

            if (resultProperty.PropertyMapping == null)
            {
                Debug.Assert(resultProperty.PropertyGroupMapping == null);
                var instance = Activator.CreateInstance(capabilityType);

                var definedProps = new List<(PropertyInfo, DotvvmProperty)>();
                var definedPGroups = new List<(PropertyInfo, DotvvmPropertyGroup)>();

                foreach (var prop in properties)
                {
                    var defaultValue = ValueOrBinding<object>.FromBoxedValue(prop.GetValue(instance));
                    var attrProvider = CombinedDataContextAttributeProvider.Create(resultProperty.AttributeProvider, prop);
                    var newProperty = InitializeArgument(attrProvider, globalPrefix + prop.Name, prop.PropertyType, declaringType, resultProperty, defaultValue);

                    if (newProperty is DotvvmProperty p)
                        definedProps.Add((prop, p));
                    else if (newProperty is DotvvmPropertyGroup g)
                        definedPGroups.Add((prop, g));
                }

                resultProperty.PropertyMapping = definedProps.ToImmutableArray();
                resultProperty.PropertyGroupMapping = definedPGroups.ToImmutableArray();
            }

            var accessors = CodeGeneration.CreateCapabilityAccessors(resultProperty);
            resultProperty.Getter = accessors.getter.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);
            resultProperty.Setter = accessors.setter.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);
        }

        static string ToPascalCase(string p) =>
            p.Length > 0 && char.IsLower(p[0]) ?
                char.ToUpperInvariant(p[0]) + p.Substring(1) :
                p;

        private static readonly ParameterExpression currentControlParameter = Expression.Parameter(typeof(DotvvmBindableObject), "control");
        /// <summary> Returns DotvvmProperty, DotvvmCapabilityProperty or DotvvmPropertyGroup </summary>
        internal static IControlAttributeDescriptor InitializeArgument(ICustomAttributeProvider attributeProvider, string propertyName, Type propertyType, Type declaringType, DotvvmCapabilityProperty? declaringCapability, ValueOrBinding<object>? defaultValue)
        {
            // we need to make sure that base type is initialized, otherwise we might miss that some properties are already defined in base type
            // and we'd redefine them for the second time here (HtmlGenericControl.Id vs DotvvmControl.Id, see https://github.com/riganti/dotvvm/issues/1387)
            DefaultControlResolver.InitType(declaringType.BaseType.NotNull("declaringType.BaseType is null"));
            // if (propertyName == "ValueOrBindingNullable") throw new Exception("xx " + declaringType);
            
            var capabilityType = declaringCapability?.PropertyType;
            propertyName = ToPascalCase(propertyName).DotvvmInternString(trySystemIntern: true);

            if (attributeProvider.GetCustomAttribute<DefaultValueAttribute>() is DefaultValueAttribute defaultAttribute)
            {
                defaultValue = ValueOrBinding<object>.FromBoxedValue(defaultAttribute.Value);
            }
            var boxedDefaultValue = defaultValue?.UnwrapToObject();
            var globalPrefix = declaringCapability?.Prefix ?? "";

            // Property Group
            if (attributeProvider.GetCustomAttribute<PropertyGroupAttribute>() is PropertyGroupAttribute groupAttribute)
            {
                var elementType = Helpers.GetDictionaryElement(propertyType);
                var unwrappedType = elementType.UnwrapValueOrBinding();

                var propertyGroup = DotvvmPropertyGroup.Register(
                    declaringType,
                    groupAttribute.Prefixes.Select(p => globalPrefix + p).ToArray(),
                    propertyName,
                    unwrappedType,
                    attributeProvider,
                    boxedDefaultValue,
                    declaringCapability: declaringCapability
                );
                propertyGroup.AddUsedInCapability(declaringCapability);

                return propertyGroup;
            }
            // Control Capability
            else if (propertyType.IsDefined(typeof(DotvvmControlCapabilityAttribute)) || attributeProvider.IsDefined(typeof(DotvvmControlCapabilityAttribute), true))
            {
                var prefix = globalPrefix + attributeProvider.GetCustomAttribute<DotvvmControlCapabilityAttribute>()?.Prefix;

                DotvvmCapabilityProperty capability;
                if (Find(declaringType, propertyType, prefix) is {} existingProperty)
                {
                    checkPropertyConflict(existingProperty, propertyType);
                    capability = existingProperty;
                    capability.AddUsedInCapability(declaringCapability);
                }
                else
                {
                    capability = DotvvmCapabilityProperty.RegisterCapability(declaringType, propertyType, prefix, name: null, attributeProvider, declaringCapability);
                }
                return capability;
            }
            // Standard property
            else
            {
                var type = propertyType.UnwrapValueOrBinding();

                DotvvmProperty dotvvmProperty;
                if (DotvvmProperty.ResolveProperty(declaringType, propertyName) is {} existingProperty)
                {
                    checkPropertyConflict(existingProperty, type);
                    dotvvmProperty = existingProperty;
                }
                else
                {
                    dotvvmProperty = new DotvvmProperty(propertyName, type, declaringType, boxedDefaultValue, false, attributeProvider);
                    dotvvmProperty.OwningCapability = declaringCapability;
                    
                    var isNullable = propertyType.IsNullable() || type.IsNullable();
                    if (!defaultValue.HasValue && !isNullable)
                        dotvvmProperty.MarkupOptions._required ??= true;

                    if (typeof(IBinding).IsAssignableFrom(propertyType))
                        dotvvmProperty.MarkupOptions._allowHardCodedValue ??= false;
                    else if (!typeof(ValueOrBinding).IsAssignableFrom(propertyType.UnwrapNullableType()))
                        dotvvmProperty.MarkupOptions._allowBinding ??= false;

                    if (typeof(IDotvvmObjectLike).IsAssignableFrom(type) ||
                        typeof(ITemplate).IsAssignableFrom(type) ||
                        typeof(IEnumerable<IDotvvmObjectLike>).IsAssignableFrom(type))
                        dotvvmProperty.MarkupOptions._mappingMode ??= MappingMode.InnerElement;

                    DotvvmProperty.Register(dotvvmProperty);
                }

                dotvvmProperty.AddUsedInCapability(declaringCapability);

                return dotvvmProperty;
            }

            void checkPropertyConflict(DotvvmProperty existingProperty, Type newPropertyType)
            {
                CheckPropertyConflict(existingProperty, newPropertyType, declaringType, declaringCapability);
            }
        }
        static void CheckPropertyConflict(DotvvmProperty existingProperty, Type newPropertyType, Type declaringType, DotvvmCapabilityProperty? declaringCapability)
        {
            if (existingProperty is DotvvmPropertyAlias alias)
                DotvvmPropertyAlias.Resolve(alias);

            string error = "";
            // same type
            if (newPropertyType != existingProperty.PropertyType)
                error += $" The properties have different types: '{newPropertyType.ToCode()}' vs '{existingProperty.PropertyType.ToCode()}'.";

            // the existing property must be declared above this one
            if (existingProperty.OwningCapability is {} existingCapability && declaringCapability is object)
            {
                var commonAncestor = existingCapability.ThisAndOwners().Intersect(declaringCapability.ThisAndOwners()).FirstOrDefault();
                var commonAncestorStr = commonAncestor?.PropertyType.ToCode(stripNamespace: true) ?? declaringType.ToCode(stripNamespace: true);
                if (!declaringCapability.IsOwnedByCapability(existingCapability))
                    error += $" The property is declared in capabilities {existingCapability.Name} and {declaringCapability.Name} - to resolve the conflict declare the property in {commonAncestorStr}.";
            }
            // It is allowed to share property when it's declared in the control (existingCapability is null)
            // And it's allowed to share property with GetContents parameter (declaringCapability is null)

            if (error.Length == 0)
                return;

            var capabilityType = declaringCapability?.PropertyType;
            var capabilityHelp = capabilityType is null ? "" : $"The property is being defined because it is in {capabilityType.Name}, you can set prefix of the capability to prevent conflict. ";
            var compositeHelp =
                capabilityType is null && typeof(CompositeControl).IsAssignableFrom(declaringType) ?
                $"The property is being defined because parameter of it's name is defined in the {declaringType}.GetContents method. " : "";
            throw new Exception($"Cannot define property {declaringType.ToCode()}.{existingProperty.Name} as it already exists.{error} {capabilityHelp}");
        }

        public record InvalidCapabilityTypeException(DotvvmCapabilityProperty Capability, string Reason)
            : DotvvmExceptionBase(Reason, RelatedProperty: Capability)
        {
            public override string Message =>
                $"Capability {Capability.PropertyType.ToCode(stripNamespace: true)} {Reason}. It was registered as capability property in {Capability.OwningCapability?.Name ?? Capability.DeclaringType.ToCode(stripNamespace: true)}.";
        }

        public record CapabilityAlreadyExistsException(DotvvmCapabilityProperty OldCapability, bool CheckedAfterContentRegistration)
            : DotvvmExceptionBase(RelatedProperty: OldCapability)
        {
            public override string Message { get {
                var postContentHelp = CheckedAfterContentRegistration ? $"It seems that the capability contains a property of the same type, which leads to the conflict. " : "";

                return $"Capability of type {OldCapability.PropertyType.ToCode(stripNamespace: true)} is already registered on control {OldCapability.DeclaringType.ToCode(stripNamespace: true)} with prefix '{OldCapability.Prefix}'. {postContentHelp}If you want to register the capability multiple times, consider giving it a different prefix.";
            } }
        }
    }

    /// <summary> This attribute is used for marking a DotVVM capability type. It can be also used to mark a capability property inside another capability. </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class DotvvmControlCapabilityAttribute : Attribute
    {
        public string Prefix { get; }
        public DotvvmControlCapabilityAttribute(string prefix = "")
        {
            this.Prefix = prefix;
        }
    }
}
