using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Binding
{
    /// <summary> Non-generic variant of <see cref="ValueOrBinding{T}" />. Represents either a binding or a constant value. In TypeScript this would be object | <see cref="IBinding"/>  </summary>
    public interface ValueOrBinding
    {
        IBinding? BindingOrDefault { get; }
        object? BoxedValue { get; }
    }

    /// <summary> Represents either a binding or a constant value. In TypeScript this would be <typeparamref name="T"/> | <see cref="IBinding"/>. Note that `default(<see cref="ValueOrBinding{T}" />)` is the same as `new <see cref="ValueOrBinding{T}" />(default(T))` </summary>
    public struct ValueOrBinding<T> : ValueOrBinding
    {
        private readonly IBinding? binding;
        [AllowNull]
        private readonly T value;

        private ValueOrBinding(IBinding? binding, [AllowNull] T value)
        {
            this.binding = binding;
            this.value = value;
        }

        /// <summary> Creates new ValueOrBinding which contains the specified binding. Will throw an exception if the binding's result type is not assignable to <typeparamref name="T"/> </summary>
        public ValueOrBinding(IBinding binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            BindingHelper.InvalidBindingTypeException.CheckType(binding, typeof(T));
            this.binding = binding;
            this.value = default;
        }

        /// <summary> Creates new ValueOrBinding which contains the specified binding. </summary>
        public ValueOrBinding(IStaticValueBinding<T> binding)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            // result type check is unnecessary when binding is generic
            this.binding = binding;
            this.value = default;
        }

        /// <summary> Creates new ValueOrBinding which contains the specified value. Note that there is an implicit conversion for this, so calling the constructor explicitly may be unnecessary. </summary>
        [DebuggerStepThrough]
        public ValueOrBinding(T value)
        {
            this.value = value;
            this.binding = default;
        }

        /// <summary> Creates a ValueOrBinding from raw object. If the object is IBinding, the ValueOrBinding will <see cref="HasBinding"/> == true, otherwise it will <see cref="HasValue" /> == true. </summary>
        public static ValueOrBinding<T> FromBoxedValue(object? value) =>
            value is IStaticValueBinding<T> bindingS ? new ValueOrBinding<T>(bindingS) :
            value is IBinding binding ? new ValueOrBinding<T>(binding) :
            value is ValueOrBinding vob ? new ValueOrBinding<T>(vob.BindingOrDefault, (T)vob.BoxedValue!) :
            new ValueOrBinding<T>((T)value!);

        /// <summary> If the binding <see cref="HasValue" />, returns it. If it <see cref="HasBinding" />, evaluates it on the <paramref name="control"/> and returns the result. </summary>
        [return: MaybeNull]
        public T Evaluate(DotvvmBindableObject control) =>
            binding is object ? (T)binding.GetBindingValue(control)! : value;

        /// <summary> Returns the value as object if this <see cref="HasValue"/> or `default(T)` if this <see cref="HasBinding"/>. </summary>
        public T ValueOrDefault => value;
        /// <summary> Returns the binding if this <see cref="HasBinding"/>, or null if this <see cref="HasValue"/> or `default(T)`. </summary>
        public IBinding? BindingOrDefault => binding;
        /// <summary> Returns the value as object if this <see cref="HasValue"/> or null if this <see cref="HasBinding"/>. </summary>
        public object? BoxedValue => HasValue ? BoxingUtils.BoxGeneric(value) : null;

        /// <summary> If this ValueOrBinding contains value. </summary>
        [MemberNotNullWhenAttribute(false, "BindingOrDefault", "binding")]
        public bool HasValue => binding is null;

        /// <summary> If this ValueOrBinding contains binding. </summary>
        [MemberNotNullWhenAttribute(true, "BindingOrDefault", "binding")]
        public bool HasBinding => binding is object;

        /// <summary> Gets a binding or throws an exception if the ValueOrBinding contains a value. </summary>
        public IBinding GetBinding() =>
            HasBinding ? binding : throw new DotvvmControlException($"Binding was expected but ValueOrBinding<{typeof(T).Name}> contains a value: {value}.");

        /// <summary> Gets a value from ValueOrBinding or throws an exception if the it contains a binding. To evaluate the binding use the <see cref="Evaluate(DotvvmBindableObject)" /> method. </summary>
        public T GetValue() =>
            HasValue ? value : throw new DotvvmControlException($"Value was expected but ValueOrBinding<{typeof(T).Name}> contains a binding: {binding}.") { RelatedBinding = binding };

        /// <summary> Returns a ValueOrBinding with new type T which is a base type of the old T2 </summary>
        public static ValueOrBinding<T> DownCast<T2>(ValueOrBinding<T2> createFrom)
            where T2 : T => new ValueOrBinding<T>(createFrom.binding, createFrom.value!);

        /// <summary> Returns a ValueOrBinding with new type T which is a base type of the old T2 </summary>
        public static ValueOrBinding<T> UpCast(ValueOrBinding createFrom) =>
            createFrom.BindingOrDefault != null ?
            new ValueOrBinding<T>(createFrom.BindingOrDefault) :
            new ValueOrBinding<T>((T)createFrom.BoxedValue!);

        /// <summary> Returns a ValueOrBinding with new type T2 which is a derived type of the old T. Will throw an exception if the conversion is not possible. </summary>
        public ValueOrBinding<T2> UpCast<T2>()
            where T2 : T =>
            this.binding != null ?
            new ValueOrBinding<T2>(this.binding) :
            new ValueOrBinding<T2>((T2)this.value!);

        /// <summary> Returns a Javascript (knockout) expression representing this value or this binding. </summary>
        public ParametrizedCode GetParametrizedJsExpression(DotvvmBindableObject control, bool unwrapped = false) =>
            ProcessValueBinding(control,
                value => new ParametrizedCode(JsonSerializer.Serialize(value, DefaultSerializerSettingsProvider.Instance.Settings), OperatorPrecedence.Max),
                binding => binding.GetParametrizedKnockoutExpression(control, unwrapped)
            );

        /// <summary> Returns a Javascript (knockout) expression representing this value or this binding. The parameters are set to defaults, so knockout context is $context, view model is $data and both are available as global. </summary>
        public string GetJsExpression(DotvvmBindableObject control, bool unwrapped = false) =>
            ProcessValueBinding(control,
                value => JsonSerializer.Serialize(value, DefaultSerializerSettingsProvider.Instance.Settings),
                binding => binding.GetKnockoutBindingExpression(control, unwrapped)
            );

        /// <summary> Simple helper which invokes <paramref name="processValue"/> if this HasValue and <paramref name="processBinding"/> if it HasBinding. </summary>
        public void Process(Action<T> processValue, Action<IBinding> processBinding)
        {
            if (binding != null)
                processBinding?.Invoke(binding);
            else processValue?.Invoke(value);
        }

        /// <summary> Simple helper which invokes <paramref name="processValue"/> if this HasValue and <paramref name="processBinding"/> if it HasBinding. </summary>
        public TResult Process<TResult>(Func<T, TResult> processValue, Func<IBinding, TResult> processBinding)
        {
            if (binding != null)
                return processBinding(binding);
            else return processValue(value);
        }

        /// <summary> Simple helper which invokes <paramref name="processValue"/> if this HasValue or if the binding is a resource binding. Invokes <paramref name="processBinding"/> if it HasBinding and the binding is <see cref="IValueBinding" />. </summary>
        public void ProcessValueBinding(DotvvmBindableObject control, Action<T> processValue, Action<IValueBinding> processBinding)
        {
            if (binding == null)
                processValue?.Invoke(value);
            else if (binding is IValueBinding valueBinding)
                processBinding?.Invoke(valueBinding);
            else
                processValue?.Invoke(this.Evaluate(control)!);
        }

        /// <summary> Simple helper which invokes <paramref name="processValue"/> if this HasValue or if the binding is a resource binding. Invokes <paramref name="processBinding"/> if it HasBinding and the binding is <see cref="IValueBinding" />. </summary>
        public TResult ProcessValueBinding<TResult>(DotvvmBindableObject control, Func<T, TResult> processValue, Func<IValueBinding, TResult> processBinding)
        {
            if (binding == null)
                return processValue(value);
            else if (binding is IValueBinding valueBinding)
                return processBinding(valueBinding);
            else
                return processValue(this.Evaluate(control)!);
        }

        /// <summary> If this contains a `resource` binding, it is evaluated and its value placed in <see cref="ValueOrDefault" /> property. `value`, and all other bindings are untouched and remain in the <see cref="BindingOrDefault"/> property. </summary>
        public ValueOrBinding<T?> EvaluateResourceBinding(DotvvmBindableObject control)
        {
            if (binding is null or IValueBinding or not IStaticValueBinding) return this;

            var value = this.Evaluate(control);
            return new ValueOrBinding<T?>(value);
        }

        public static explicit operator ValueOrBinding<T>(T val) => new ValueOrBinding<T>(val);

        public const string EqualsDisabledReason = "Equals is disabled on ValueOrBinding<T> as it may lead to unexpected behavior. Please use object.ReferenceEquals for reference comparison or evaluate the ValueOrBinding<T> and compare the value. Or use IsNull/NotNull for nullchecks on bindings.";
        [Obsolete(EqualsDisabledReason, error: true)]
        public static bool operator ==(ValueOrBinding<T> a, ValueOrBinding<T> b) =>
            throw new NotSupportedException(EqualsDisabledReason);
        [Obsolete(EqualsDisabledReason, error: true)]
        public static bool operator !=(ValueOrBinding<T> a, ValueOrBinding<T> b) =>
            throw new NotSupportedException(EqualsDisabledReason);

#pragma warning disable CS0809
        [Obsolete(EqualsDisabledReason, error: true)]
        public override bool Equals(object? obj) => throw new NotSupportedException(EqualsDisabledReason);

        [Obsolete(EqualsDisabledReason, error: true)]
        public override int GetHashCode() => throw new NotSupportedException(EqualsDisabledReason);
#pragma warning restore CS0809

        public override string? ToString() =>
            HasBinding ? binding.ToString() :
            value is null ? "null" : value.ToString();

    }
}
