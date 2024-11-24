using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Controls
{

    [ContainsDotvvmProperties]
    [ControlMarkupOptions(AllowContent = true)]
    [System.Text.Json.Serialization.JsonConverter(typeof(DotvvmControlDebugJsonConverter))]
    public abstract class DotvvmBindableObject: IDotvvmObjectLike
    {

        private static readonly ConcurrentDictionary<Type, DotvvmProperty[]> declaredProperties = new ConcurrentDictionary<Type, DotvvmProperty[]>();


        internal DotvvmControlProperties properties;

        /// <summary>
        /// Gets the collection of control property values.
        /// </summary>
        public DotvvmPropertyDictionary Properties =>
            new DotvvmPropertyDictionary(this);


        /// <summary>
        /// Gets or sets whether this control should be rendered on the server.
        /// </summary>
        public virtual bool RenderOnServer
        {
            get { return (RenderMode)GetValue(RenderSettings.ModeProperty)! == RenderMode.Server; }
        }

        /// <summary>
        /// Gets the parent control.
        /// </summary>
        [MarkupOptions(MappingMode = MappingMode.Exclude)]
        public DotvvmBindableObject? Parent { get; set; }

        // WORKAROUND: Roslyn is unable to cache the delegate itself
        private static Func<Type, DotvvmProperty[]> _dotvvmProperty_ResolveProperties = DotvvmProperty.ResolveProperties;

        /// <summary>
        /// Gets all properties declared on this class or on any of its base classes.
        /// </summary>
        protected DotvvmProperty[] GetDeclaredProperties()
        {
            return declaredProperties.GetOrAdd(GetType(), _dotvvmProperty_ResolveProperties);
        }

        /// <summary>
        /// Determines whether the specified property is set.
        /// </summary>
        public bool IsPropertySet(DotvvmProperty property, bool inherit = true)
        {
            return property.IsSet(this, inherit);
        }


        /// <summary>
        /// Gets or sets a data context for the control and its children. All value and command bindings are evaluated in context of this value.
        /// The DataContext is null in client-side templates.
        /// </summary>
        [BindingCompilationRequirements(
                optional: new[] { typeof(Binding.Properties.SimplePathExpressionBindingProperty) })]
        [MarkupOptions(AllowHardCodedValue = false)]
        public object? DataContext
        {
            get {
                for (var c = this; c != null; c = c.Parent)
                {
                    if (c.properties.TryGet(DotvvmBindableObject.DataContextProperty, out var value))
                    {
                        return c.EvalPropertyValue(DotvvmBindableObject.DataContextProperty, value);
                    }
                }
                return null;
            }
            set { this.properties.Set(DataContextProperty, value); }
        }

        DotvvmBindableObject IDotvvmObjectLike.Self => this;

        public static readonly DotvvmProperty DataContextProperty =
            DotvvmProperty.Register<object, DotvvmBindableObject>(c => c.DataContext, isValueInherited: true);

        /// <summary> Returns the value of the specified property. If the property contains a binding, it is evaluted. </summary>
        [return: MaybeNull]
        public T GetValue<T>(DotvvmProperty property, bool inherit = true)
        {
            return (T)GetValue(property, inherit)!;
        }

        /// <summary> If the object is IBinding and the property is not of type IBinding, it is evaluated. </summary>
        internal object? EvalPropertyValue(DotvvmProperty property, object? value)
        {
            if (property.IsBindingProperty) return value;
            if (value is IBinding)
            {
                DotvvmBindableObject control = this;
                // DataContext is always bound to it's parent, setting it right here is a bit faster
                if (property == DataContextProperty)
                    control = control.Parent ?? throw new DotvvmControlException(this, "Cannot set DataContext binding on the root control");
                // handle binding
                if (value is IStaticValueBinding binding)
                {
                    value = binding.Evaluate(control);
                }
                else if (value is ICommandBinding command)
                {
                    value = command.GetCommandDelegate(control);
                }
                else
                {
                    throw new NotSupportedException($"Cannot evaluate binding {value} of type {value.GetType().Name}.");
                }
            }
            return value;
        }

        /// <summary>
        /// Gets the value of a specified property. If the property contains a binding, it is evaluted.
        /// </summary>
        public virtual object? GetValue(DotvvmProperty property, bool inherit = true) =>
            EvalPropertyValue(property, GetValueRaw(property, inherit));

        /// <summary>
        /// Gets the value or a binding object for a specified property.
        /// </summary>
        public virtual object? GetValueRaw(DotvvmProperty property, bool inherit = true)
        {
            return property.GetValue(this, inherit);
        }

        /// <summary> For internal use, public because it's used from our generated code. If want to use it, create the arguments using <see cref="PropertyImmutableHashtable.CreateTableWithValues{T}(DotvvmProperty[], T[])" /> </summary>
        public void MagicSetValue(DotvvmProperty[] keys, object[] values, int hashSeed)
        {
            this.properties.AssignBulk(keys, values, hashSeed);
        }

        /// <summary> Sets the value of a specified property. </summary>
        public void SetValue<T>(DotvvmProperty property, ValueOrBinding<T> valueOrBinding)
        {
            if (valueOrBinding.BindingOrDefault == null)
                this.SetValue(property, valueOrBinding.BoxedValue);
            else
                this.SetBinding(property, valueOrBinding.BindingOrDefault);
        }

        /// <summary> Gets the value of a specified property. Bindings are always returned, not evaluated. </summary>
        public ValueOrBinding<T> GetValueOrBinding<T>(DotvvmProperty property, bool inherit = true)
        {
            var value = this.GetValueRaw(property, inherit);
            if (value is IBinding binding)
                return new ValueOrBinding<T>(binding);
            else return new ValueOrBinding<T>((T)value!);
        }

        /// <summary>
        /// Sets the value of a specified property.
        /// </summary>
        public virtual void SetValue(DotvvmProperty property, object? value)
        {
            // "unbox" ValueOrBinding instances
            value = ValueOrBindingExtensions.UnwrapToObject(value);

            SetValueRaw(property, value);
        }

        /// <summary>
        /// Sets the value of a specified property.
        /// </summary>
        public void SetValue<T>(DotvvmProperty property, T value)
            where T: struct
        {
            SetValue(property, BoxingUtils.BoxGeneric(value));
        }

        /// <summary> Sets the value of specified property by updating the view model this property is bound to. Throws if the property does not contain binding </summary>
        public void SetValueToSource(DotvvmProperty property, object? value)
        {
            if (value is IBinding newBinding)
                throw new DotvvmControlException(this, $"Cannot set binding {value} to source.") { RelatedBinding = newBinding, RelatedProperty = property };
            var binding = GetBinding(property);
            if (binding is null)
                throw new DotvvmControlException(this, $"Property {property} does not contain binding, so it's source cannot be updated.") { RelatedProperty = property };
            if (binding is not IUpdatableValueBinding updatableValueBinding)
                throw new DotvvmControlException(this, $"Cannot set source of binding {value}, it does not implement IUpdatableValueBinding.") { RelatedBinding = binding, RelatedProperty = property };
            
            updatableValueBinding.UpdateSource(value, this);
        }

        /// <summary>
        /// Sets the value or a binding to the specified property.
        /// </summary>
        public void SetValueRaw(DotvvmProperty property, object? value)
        {
            property.SetValue(this, value);
        }

        /// <summary>
        /// Gets the binding set to a specified property. Returns null if the property is not set or if the value is not a binding.
        /// </summary>
        public IBinding? GetBinding(DotvvmProperty property, bool inherit = true)
            => GetValueRaw(property, inherit) as IBinding;

        /// <summary>
        /// Gets the value binding set to a specified property. Returns null if the property is not a binding, throws if the binding some kind of command.
        /// </summary>
        public IValueBinding? GetValueBinding(DotvvmProperty property, bool inherit = true)
        {
            var binding = GetBinding(property, inherit);
            if (binding != null && !(binding is IStaticValueBinding)) // throw exception on incompatible binding types
            {
                throw new BindingHelper.BindingNotSupportedException(binding) { RelatedControl = this };
            }
            return binding as IValueBinding;
        }

        /// <summary>
        /// Gets the value binding set to a specified property. Returns null if the property is not a binding, throws if the binding some kind of command.
        /// </summary>
        public IStaticValueBinding? GetValueOrResourceBinding(DotvvmProperty property, bool inherit = true)
        {
            var binding = GetBinding(property, inherit);
            if (binding is null)
                return null;
            if (binding is not IStaticValueBinding valueBinding)
            {
                // throw exception on incompatible binding types
                throw new BindingHelper.BindingNotSupportedException(binding) { RelatedControl = this };
            }
            return valueBinding;
        }

        /// <summary> Returns a Javascript (knockout) expression representing value or binding of this property. </summary>
        public ParametrizedCode GetJavascriptValue(DotvvmProperty property, bool inherit = true) =>
            GetValueOrBinding<object>(property, inherit).GetParametrizedJsExpression(this);

        /// <summary>
        /// Gets the command binding set to a specified property. Returns null if the property is not a binding, throws if the binding is not command, controlCommand or staticCommand.
        /// </summary>
        public ICommandBinding? GetCommandBinding(DotvvmProperty property, bool inherit = true)
        {
            var binding = GetBinding(property, inherit);
            if (binding != null && !(binding is ICommandBinding))
            {
                throw new BindingHelper.BindingNotSupportedException(binding) { RelatedControl = this };
            }
            return binding as ICommandBinding;
        }

        /// <summary>
        /// Sets the binding to a specified property.
        /// </summary>
        public void SetBinding(DotvvmProperty property, IBinding? binding)
        {
            SetValueRaw(property, binding);
        }


        /// <summary>
        /// Gets the hierarchy of all DataContext bindings from the root to current control.
        /// </summary>
        [Obsolete]
        internal IEnumerable<IValueBinding> GetDataContextHierarchy()
        {
            var bindings = new List<IValueBinding>();
            DotvvmBindableObject? current = this;
            while (current != null)
            {
                var binding = current.GetValueBinding(DataContextProperty, false);
                if (binding != null)
                {
                    bindings.Add(binding);
                }
                current = current.Parent;
            }

            bindings.Reverse();
            return bindings;
        }

        /// <summary>
        /// Gets the closest control binding target. Returns null if the control is not found.
        /// </summary>
        public DotvvmBindableObject? GetClosestControlBindingTarget()
        {
            var c = this;
            while (c != null)
            {
                if (c.properties.TryGet(Internal.IsControlBindingTargetProperty, out var x) && (bool)x!)
                {
                    return c;
                }
                c = c.Parent;
            }
            return null;
        }

        /// <summary>
        /// Gets the closest control binding target and returns number of DataContext changes since the target. Returns null if the control is not found.
        /// </summary>
        public DotvvmBindableObject? GetClosestControlBindingTarget(out int numberOfDataContextChanges) =>
            (Parent ?? this).GetClosestWithPropertyValue(
                out numberOfDataContextChanges,
                (control, _) => control.properties.TryGet(Internal.IsControlBindingTargetProperty, out var x) && (bool)x!);

        /// <summary>
        /// Gets the closest control binding target and returns number of DataContext changes since the target. Returns null if the control is not found.
        /// </summary>
        public DotvvmBindableObject? GetClosestControlValidationTarget(out int numberOfDataContextChanges) =>
            GetClosestWithPropertyValue(out numberOfDataContextChanges, (c, _) => c.IsPropertySet(Validation.TargetProperty, false), includeDataContextChangeOnMatchedControl: false);


        /// <summary>
        /// Gets the closest control with specified property value and returns number of DataContext changes since the target. Returns null if the control is not found.
        /// </summary>
        public DotvvmBindableObject? GetClosestWithPropertyValue(out int numberOfDataContextChanges, Func<DotvvmBindableObject, DotvvmProperty?, bool> filterFunction, bool includeDataContextChangeOnMatchedControl = true, DotvvmProperty? delegateValue = null)
        {
            DotvvmBindableObject? current = this;
            numberOfDataContextChanges = 0;
            while (current != null)
            {
                var isMatched = false;
                if (current.GetValueBinding(DataContextProperty, false) != null)
                {
                    if (current.HasBinding(DataContextProperty) || current.HasBinding(Internal.PathFragmentProperty))
                    {
                        numberOfDataContextChanges++;
                        isMatched = true;
                    }
                }
                if (filterFunction(current, delegateValue))
                {
                    if (isMatched && !includeDataContextChangeOnMatchedControl)
                    {
                        numberOfDataContextChanges--;
                    }

                    break;
                }

                current = current.Parent;
            }
            return current;
        }

        /// <summary> if this property contains any kind of binding. Note that the property value is not inherited. </summary>
        public bool HasBinding(DotvvmProperty property)
        {
            return properties.TryGet(property, out var value) && value is IBinding;
        }
        /// <summary> if this property contains value binding. Note that the property value is not inherited. </summary>
        public bool HasValueBinding(DotvvmProperty property)
        {
            return properties.TryGet(property, out var value) && value is IValueBinding;
        }

        /// <summary> if this property contains binding of the specified type. Note that the property value is not inherited. </summary>
        public bool HasBinding<TBinding>(DotvvmProperty property)
            where TBinding : IBinding
        {
            return properties.TryGet(property, out var value) && value is TBinding;
        }

        /// <summary>
        /// Gets all bindings set on the control (excluding BindingProperties).
        /// </summary>
        public IEnumerable<KeyValuePair<DotvvmProperty, IBinding>> GetAllBindings()
        {
            return Properties.Where(p => p.Value is IBinding)
                .Select(p => new KeyValuePair<DotvvmProperty, IBinding>(p.Key, (IBinding)p.Value!));
        }

        /// <summary>
        /// Gets all ancestors of this control starting with the parent.
        /// </summary>
        /// <param name="includingThis">Returns also the caller control</param>
        /// <param name="onlyWhenInChildren">Only enumerate until the parent has this control in <see cref="DotvvmControl.Children" />. Note that it may have a non-trivial performance penalty</param>
        public IEnumerable<DotvvmBindableObject> GetAllAncestors(bool includingThis = false, bool onlyWhenInChildren = false)
        {
            var ancestor = includingThis ? this : Parent;
            while (ancestor != null)
            {
                yield return ancestor;
                if (onlyWhenInChildren)
                {
                    if (!(ancestor.Parent is DotvvmControl parentControl && parentControl.Children.Contains(ancestor)))
                        yield break;
                }
                ancestor = ancestor.Parent;
            }
        }

        /// <summary>
        /// Gets the root of the control tree. The the control is properly rooted, the result value will be of type <see cref="Infrastructure.DotvvmView" />
        /// </summary>
        public DotvvmBindableObject GetRoot()
        {
            if (Parent == null) return this;
            return GetAllAncestors().Last();
        }

        /// <summary> Does a deep clone of the control. </summary>
        protected internal virtual DotvvmBindableObject CloneControl()
        {
            var newThis = (DotvvmBindableObject)this.MemberwiseClone();
            this.properties.CloneInto(ref newThis.properties);
            return newThis;
        }

        /// <summary>
        /// Gets the logical children of this control (including controls that are not in the visual tree but which can contain command bindings).
        /// </summary>
        public virtual IEnumerable<DotvvmBindableObject> GetLogicalChildren()
        {
            return Enumerable.Empty<DotvvmBindableObject>();
        }

        /// <summary>
        /// Copies the value of a property from this <see cref="DotvvmBindableObject"/> (source) to a property of another <see cref="DotvvmBindableObject"/> (target).
        /// </summary>
        /// <exception cref="DotvvmControlException">Gets thrown if copying fails and <paramref name="throwOnFailure"/> is set to true</exception>
        /// <param name="sourceProperty">The <see cref="DotvvmProperty"/> whose value will be copied</param>
        /// <param name="target">The <see cref="DotvvmBindableObject"/> that holds the value of the <paramref name="targetProperty"/></param>
        /// <param name="targetProperty">The <see cref="DotvvmProperty"/> to which <paramref name="sourceProperty"/> will be copied</param>
        /// <param name="throwOnFailure">Determines whether to throw an exception if copying fails</param>
        protected internal void CopyProperty(DotvvmProperty sourceProperty, DotvvmBindableObject target, DotvvmProperty targetProperty, bool throwOnFailure = false)
        {
            var targetOptions = targetProperty.MarkupOptions;
            if (throwOnFailure && !targetOptions.AllowBinding && !targetOptions.AllowHardCodedValue)
            {
                throw new DotvvmControlException(this, $"TargetProperty: {targetProperty.FullName} doesn't allow bindings nor hard coded values");
            }

            if (IsPropertySet(sourceProperty))
            {
                var sourceValue = GetValueRaw(sourceProperty);
                if ((targetOptions.AllowBinding || sourceValue is not IBinding) &&
                    (targetOptions.AllowHardCodedValue || sourceValue is IBinding))
                {
                    target.SetValueRaw(targetProperty, sourceValue);
                }
                else if (targetOptions.AllowHardCodedValue)
                {
                    target.SetValue(targetProperty, EvalPropertyValue(sourceProperty, sourceValue));
                }
                else if (throwOnFailure)
                {
                    throw new DotvvmControlException(this, $"Value of {sourceProperty.FullName} couldn't be copied to targetProperty: {targetProperty.FullName}, because {targetProperty.FullName} does not support hard coded values.");
                }
            }

            else if (throwOnFailure)
            {
                throw new DotvvmControlException(this, $"Value of {sourceProperty.FullName} couldn't be copied to targetProperty: {targetProperty.FullName}, because {sourceProperty.FullName} is not set.");
            }
        }

        // TODO: make public in next major version
        internal void CopyPropertyRaw(DotvvmProperty sourceProperty, DotvvmBindableObject target, DotvvmProperty targetProperty)
        {
            if (IsPropertySet(sourceProperty))
            {
                target.SetValueRaw(targetProperty, GetValueRaw(sourceProperty));
            }
        }
    }
}
