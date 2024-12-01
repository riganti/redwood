using DotVVM.Framework.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Compilation.Javascript;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation.ControlTree;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using System.Diagnostics.CodeAnalysis;
using DotVVM.Framework.Utils;
using DotVVM.Framework.Compilation.Binding;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Runtime;
using FastExpressionCompiler;
using System.Diagnostics;
using System.Reflection;

namespace DotVVM.Framework.Binding
{
    public static partial class BindingHelper
    {
        /// <summary> Gets the binding property identified by the type. The result may be null, if <paramref name="errorMode"/> is <see cref="ErrorHandlingMode.ReturnNull">ReturnNul</see> This method should always return the same result and should run fast (may rely on caching, so first call might not be that fast). </summary>
        [return: MaybeNull]
        public static T GetProperty<T>(this IBinding binding, ErrorHandlingMode errorMode) => (T)binding.GetProperty(typeof(T), errorMode)!;
        /// <summary> Gets the binding property identified by the type. This method should always return the same result and should run fast (may rely on caching, so first call might not be that fast). </summary>
        public static T GetProperty<T>(this IBinding binding) => GetProperty<T>(binding, ErrorHandlingMode.ThrowException)!;

        [Obsolete]
        public static string GetKnockoutBindingExpression(this IValueBinding binding) =>
            JavascriptTranslator.FormatKnockoutScript(binding.KnockoutExpression);
        /// <summary>
        /// Gets the javascript translation of the binding adjusted to the `currentControl`s DataContext
        /// </summary>
        public static string GetKnockoutBindingExpression(this IValueBinding binding, DotvvmBindableObject currentControl, bool unwrapped = false) =>
            (unwrapped ? binding.UnwrappedKnockoutExpression : binding.KnockoutExpression)
            .FormatKnockoutScript(currentControl, binding);

        /// <summary>
        /// Gets the javascript translation of the binding adjusted to the `currentControl`s DataContext, returned value is ParametrizedCode, so it can be further adjusted
        /// </summary>
        public static ParametrizedCode GetParametrizedKnockoutExpression(this IValueBinding binding, DotvvmBindableObject currentControl, bool unwrapped = false) =>
            JavascriptTranslator.AdjustKnockoutScriptContext(unwrapped ? binding.UnwrappedKnockoutExpression : binding.KnockoutExpression, dataContextLevel: FindDataContextTarget(binding, currentControl).stepsUp);

        /// <summary>
        /// Adjusts the knockout expression to `currentControl`s DataContext like it was translated in `currentBinding`s context
        /// </summary>
        public static string FormatKnockoutScript(this ParametrizedCode code, DotvvmBindableObject currentControl, IBinding currentBinding) =>
            JavascriptTranslator.FormatKnockoutScript(code, dataContextLevel: FindDataContextTarget(currentBinding, currentControl).stepsUp);

        /// <summary>
        /// Adjusts the knockout expression to `currentControl`s DataContext like it was translated in `currentBinding`s context
        /// </summary>
        public static string FormatKnockoutScript(this ParametrizedCode code, DotvvmBindableObject currentControl, IBinding currentBinding, int additionalDataContextSteps) =>
            JavascriptTranslator.FormatKnockoutScript(code, dataContextLevel: FindDataContextTarget(currentBinding, currentControl).stepsUp + additionalDataContextSteps);

        /// <summary>
        /// Gets Internal.PathFragmentProperty or DataContext.KnockoutExpression. Returns null if none of these is set.
        /// </summary>
        public static string? GetDataContextPathFragment(this DotvvmBindableObject currentControl) =>
            currentControl.properties.TryGet(Internal.PathFragmentProperty, out var pathFragment) && pathFragment is string pathFragmentStr ? pathFragmentStr :
            currentControl.properties.TryGet(DotvvmBindableObject.DataContextProperty, out var dataContext) && dataContext is IValueBinding binding ?
                binding.GetProperty<SimplePathExpressionBindingProperty>()
                .Code.FormatKnockoutScript(currentControl, binding) :
            null;


        // PERF: maybe safe last GetValue's target/binding to ThreadLocal variable, so the path does not have to be traversed twice
        /// <summary>
        /// Finds expected context control of the `binding` and returns (parent index of the correct DataContext, control in the correct context)
        /// </summary>
        public static (int stepsUp, DotvvmBindableObject target) FindDataContextTarget(this IBinding binding, DotvvmBindableObject control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control), $"Cannot evaluate binding without any dataContext.");
            var bindingContext = binding.DataContext;
            return FindDataContextTarget(control, bindingContext, binding);
        }

        internal static (int stepsUp, DotvvmBindableObject target) FindDataContextTarget(DotvvmBindableObject control, DataContextStack? bindingContext, object? contextObject)
        {
            var controlContext = control.GetDataContextType();
            if (bindingContext == null || controlContext == null || controlContext.Equals(bindingContext)) return (0, control);

            var changes = 0;
            var lastAncestorContext = controlContext;
            foreach (var a in control.GetAllAncestors(includingThis: true))
            {
                var ancestorContext = a.GetDataContextType(inherit: false);

                if (ancestorContext is null)
                    continue;

                if (!ancestorContext.ServerSideOnly &&
                    !ancestorContext.Equals(lastAncestorContext))
                {
                    // only count changes which are visible client-side
                    // server-side context are not present in the client-side stack at all, so we need to skip them here

                    // don't count changes which only extend the data context, but don't nest it

                    var isNesting = ancestorContext.IsAncestorOf(lastAncestorContext);
                    if (isNesting)
                    {
                        changes++;
                    }
#if DEBUG
                    else if (!lastAncestorContext.DataContextType.IsAssignableFrom(ancestorContext.DataContextType))
                    {
                        // this should not happen - data context type should not randomly change without nesting.
                        // we change data context stack when we get into different compilation context - a markup control
                        // but that will be always the same viewmodel type (or supertype)

                        var previousAncestor = control.GetAllAncestors(includingThis: true).TakeWhile(aa => aa != a).LastOrDefault();
                        var config = (control.GetValue(Internal.RequestContextProperty) as Hosting.IDotvvmRequestContext)?.Configuration;
                        throw new DotvvmControlException(
                            previousAncestor ?? a,
                            $"DataContext type changed from '{lastAncestorContext.DataContextType.ToCode()}' to '{ancestorContext.DataContextType.ToCode()}' without nesting. " +
                            $"{previousAncestor?.DebugString(config)} has DataContext: {lastAncestorContext}, " +
                            $"{a.DebugString(config)} has DataContext: {ancestorContext}");
                    }
#endif
                    lastAncestorContext = ancestorContext;
                }

                if (bindingContext.Equals(ancestorContext))
                    return (changes, a);

            }

            // try to get the real objects, to see which is wrong
            object?[]? dataContexts = null;
            try
            {
                dataContexts = control.GetDataContexts().ToArray();
            }
            catch { }

            throw new InvalidDataContextTypeException(control, contextObject, controlContext, bindingContext,
                ActualContextTypes: dataContexts?.Select(o => o?.GetType()).ToArray()
            );
        }

        /// <summary>
        /// Prepares DataContext hierarchy argument and executes update delegate.
        /// </summary>
        public static void ExecUpdateDelegate(this BindingUpdateDelegate func, DotvvmBindableObject contextControl, object? value)
        {
            //var control = contextControl.GetClosestControlBindingTarget();
            func(contextControl, value);
        }

        /// <summary>
        /// Prepares DataContext hierarchy argument and executes update delegate.
        /// </summary>
        public static void ExecUpdateDelegate<T>(this BindingUpdateDelegate<T> func, DotvvmBindableObject contextControl, T value)
        {
            //var control = contextControl.GetClosestControlBindingTarget();
            func(contextControl, value);
        }

        /// <summary>
        /// Prepares DataContext hierarchy argument and executes update delegate.
        /// </summary>
        public static object? ExecDelegate(this BindingDelegate func, DotvvmBindableObject contextControl)
        {
            return func(contextControl);
        }

        /// <summary>
        /// Prepares DataContext hierarchy argument and executes update delegate.
        /// </summary>
        public static T ExecDelegate<T>(this BindingDelegate<T> func, DotvvmBindableObject contextControl)
        {
            return func(contextControl);
        }

        /// <summary>
        /// Gets all data context on the path to root. Maximum count can be specified by `count`
        /// </summary>
        public static IEnumerable<object?> GetDataContexts(this DotvvmBindableObject contextControl, int count = -1)
        {
            DotvvmBindableObject? c = contextControl;
            while (c != null)
            {
                // this has O(h^2) complexity because GetValue calls another GetDataContexts,
                // but this function is used rarely - for exceptions, manually created bindings, ...
                // Normal bindings have specialized code generated in BindingCompiler
                if (c.IsPropertySet(DotvvmBindableObject.DataContextProperty, inherit: false))
                {
                    Debug.Assert(c.properties.Contains(DotvvmBindableObject.DataContextProperty), "Control claims that DataContextProperty is set, but it's not present in the properties dictionary.");
                    yield return c.GetValue(DotvvmBindableObject.DataContextProperty);
                    count--;
                }

                if (count == 0) yield break;

                c = c.Parent;
            }
        }

        /// <summary>
        /// Finds expected DataContext target in control.Ancestors() and evaluates the `binding.BindingDelegate`.
        /// </summary>
        public static object? Evaluate(this IStaticValueBinding binding, DotvvmBindableObject control)
        {
            return ExecDelegate(
                binding.BindingDelegate,
                FindDataContextTarget(binding, control).target);
        }

        /// <summary>
        /// Finds expected DataContext target in control.Ancestors() and evaluates the `binding.BindingDelegate`.
        /// </summary>
        public static T Evaluate<T>(this IStaticValueBinding<T> binding, DotvvmBindableObject control)
        {
            return ExecDelegate(
                binding.BindingDelegate,
                FindDataContextTarget(binding, control).target);
        }

        /// <summary>
        /// Writes the value to binding - bound viewModel property is updated. May throw an exception when binding does not support assignment.
        /// </summary>
        public static void UpdateSource(this IUpdatableValueBinding binding, object? value, DotvvmBindableObject control)
        {
            ExecUpdateDelegate(
                binding.UpdateDelegate,
                FindDataContextTarget(binding, control).target,
                value);
        }

        /// <summary>
        /// Writes the value to binding - bound viewModel property is updated. May throw an exception when binding does not support assignment.
        /// </summary>
        public static void UpdateSource<T>(this IUpdatableValueBinding<T> binding, T value, DotvvmBindableObject control)
        {
            ExecUpdateDelegate(
                binding.UpdateDelegate,
                FindDataContextTarget(binding, control).target,
                value);
        }

        /// <summary>
        /// Finds expected DataContext and gets the delegate from command binding.
        /// </summary>
        public static Delegate GetCommandDelegate(this ICommandBinding binding, DotvvmBindableObject control)
        {
            return (Delegate)ExecDelegate(
                binding.BindingDelegate,
                FindDataContextTarget(binding, control).target).NotNull();
        }

        /// <summary>
        /// Finds expected DataContext and gets the delegate from command binding.
        /// </summary>
        public static T GetCommandDelegate<T>(this ICommandBinding<T> binding, DotvvmBindableObject control)
        {
            return ExecDelegate(
                binding.BindingDelegate,
                FindDataContextTarget(binding, control).target);
        }

        /// <summary>
        /// Finds expected DataContext, gets the delegate from command binding and evaluates it with `args`
        /// </summary>
        public static object? Evaluate(this ICommandBinding binding, DotvvmBindableObject control, params Func<Type, object?>[] args)
        {
            var action = binding.GetCommandDelegate(control);
            if (action is null)
                // only if data context is null will our compiler return null instead of command delegate
                throw new DotvvmControlException(control, $"Cannot invoke {binding}, a referenced data context is null") { RelatedBinding = binding }; 
            if (action is Command command) return command();
            if (action is Action actionDelegate) { actionDelegate(); return null; }
            if (action is Func<Task> command2) return command2();

            var parameters = action.GetType().GetMethod("Invoke")!.GetParameters();
            if (parameters.Length != args.Length)
                throw new TargetParameterCountException($"Parameter count mismatch: received {args.Length}, but expected {parameters.Length} ({parameters.Select(p => p.ParameterType.ToCode()).StringJoin(", ")})");
            var evaluatedArgs = args.Zip(parameters, (a, p) => a(p.ParameterType)).ToArray();
            return action.DynamicInvoke(evaluatedArgs);
        }

        /// <summary>
        /// Gets DataContext-adjusted javascript that can be used for command invocation.
        /// </summary>
        public static ParametrizedCode GetParametrizedCommandJavascript(this ICommandBinding binding, DotvvmBindableObject control) =>
            JavascriptTranslator.AdjustKnockoutScriptContext(binding.CommandJavascript,
                dataContextLevel: FindDataContextTarget(binding, control).stepsUp);

        /// <summary>
        /// Gets command arguments parametrized code from the arguments collection.
        /// </summary>
        public static CodeParameterAssignment? GetParametrizedCommandArgs(DotvvmControl control, IEnumerable<object?>? argumentsCollection)
        {
            if (argumentsCollection is null) return null;
            var builder = new ParametrizedCode.Builder();
            var isFirst = true;

            builder.Add("[");
            foreach (var arg in argumentsCollection)
            {
                if (!isFirst)
                {
                    builder.Add(",");
                }

                isFirst = false;

                builder.Add(ValueOrBinding<object>.FromBoxedValue(arg).GetParametrizedJsExpression(control));
            }

            builder.Add("]");

            return builder.Build(OperatorPrecedence.Max);
        }

        public static object? GetBindingValue(this IBinding binding, DotvvmBindableObject control)
        {
            if (binding is IStaticValueBinding valueBinding)
            {
                return valueBinding.Evaluate(control);
            }
            else if (binding is ICommandBinding command)
            {
                return command.GetCommandDelegate(control);
            }
            else throw new BindingNotSupportedException(binding);
        }

        /// <summary>
        /// Creates new `TBinding` with the original DataContextStack, LocationInfo, AdditionalResolvers and BindingCompilationService.
        /// </summary>
        public static TBinding DeriveBinding<TBinding>(this TBinding binding, DataContextStack newDataContext, Expression expression, params object?[] properties)
            where TBinding : IBinding
        {
            return binding.DeriveBinding(
                properties.Concat(new object[] {
                    newDataContext,
                    new ParsedExpressionBindingProperty(expression),
                    new CastedExpressionBindingProperty(expression),
                    new ExpectedTypeBindingProperty(expression.Type),
                    new ResultTypeBindingProperty(expression.Type)
                }).ToArray()
            );
        }

        /// <summary>
        /// Creates new `TBinding` with the original DataContextStack, LocationInfo, AdditionalResolvers and BindingCompilationService.
        /// </summary>
        public static TBinding DeriveBinding<TBinding>(this TBinding binding, Expression expression, params object?[] properties)
            where TBinding : IBinding
        {
            return binding.DeriveBinding(
                properties.Concat(new object[] {
                    new ParsedExpressionBindingProperty(expression),
                    new CastedExpressionBindingProperty(expression),
                    new ExpectedTypeBindingProperty(expression.Type),
                    new ResultTypeBindingProperty(expression.Type)
                }).ToArray()
            );
        }

        /// <summary>
        /// Creates new `TBinding` with the original DataContextStack, LocationInfo, AdditionalResolvers and BindingCompilationService.
        /// </summary>
        public static TBinding DeriveBinding<TBinding>(this TBinding binding, params object?[] properties)
            where TBinding : IBinding
        {
            object?[] getContextProperties(IBinding b) =>
                new object?[] {
                    b.DataContext,
                    b.GetProperty<BindingResolverCollection>(ErrorHandlingMode.ReturnNull),
                    b.GetProperty<BindingCompilationRequirementsAttribute>(ErrorHandlingMode.ReturnNull)?.ClearRequirements(),
                    b.GetProperty<BindingErrorReporterProperty>(ErrorHandlingMode.ReturnNull),
                    b.GetProperty<DotvvmLocationInfo>(ErrorHandlingMode.ReturnNull)
                };
            var service = binding.GetProperty<BindingCompilationService>();
            var bindingType = binding.GetType();
            if (bindingType.IsGenericType)
                bindingType = bindingType.GetGenericTypeDefinition();
            return (TBinding)service.CreateBinding(bindingType, properties.Concat(getContextProperties(binding)).ToArray());
        }

        /// <summary>
        /// Caches all function evaluations in the closure based on parameter. TParam should be immutable, as it is used as Dictionary key.
        /// It thread-safe.
        /// </summary>
        public static Func<TParam, TResult> Cache<TParam, TResult>(this Func<TParam, TResult> func)
            where TParam: notnull
        {
            var cache = new ConcurrentDictionary<TParam, TResult>();
            return f => cache.GetOrAdd(f, func);
        }

        public static IStaticValueBinding GetThisBinding(this DotvvmBindableObject obj)
        {
            var dataContext = (IStaticValueBinding?)obj.GetBinding(DotvvmBindableObject.DataContextProperty);
            if (dataContext is null)
                throw new InvalidOperationException("DataContext must be set to a binding to allow creation of a {value: _this} binding");
            return (IStaticValueBinding)dataContext!.GetProperty<ThisBindingProperty>().binding;
        }

        private static readonly ConditionalWeakTable<Expression, BindingParameterAnnotation> _expressionAnnotations =
            new ConditionalWeakTable<Expression, BindingParameterAnnotation>();
        public static TExpression AddParameterAnnotation<TExpression>(this TExpression expr, BindingParameterAnnotation annotation)
            where TExpression : Expression
        {
            _expressionAnnotations.Add(expr, annotation);
            return expr;
        }

        public static BindingParameterAnnotation? GetParameterAnnotation(this Expression expr) =>
            _expressionAnnotations.TryGetValue(expr, out var annotation) ? annotation : null;

        public static TControl SetDataContextTypeFromDataSource<TControl>(this TControl control,
            IBinding dataSourceBinding) where TControl : DotvvmBindableObject =>
            control.SetDataContextType(dataSourceBinding.GetProperty<CollectionElementDataContextBindingProperty>().DataContext);

        /// <summary> Return the expected data context type for this property. Returns null if the type is unknown. </summary>
        public static DataContextStack? GetDataContextType(this DotvvmProperty property, DotvvmBindableObject obj, DataContextStack? objDataContext = null)
        {
            var dataContextType = objDataContext ?? obj.GetDataContextType();

            if (dataContextType == null)
            {
                return null;
            }

            if (property.DataContextManipulationAttribute != null)
            {
                return property.DataContextManipulationAttribute.ChangeStackForChildren(dataContextType, obj, property, (parent, changeType) => DataContextStack.Create(changeType, parent));
            }

            if (property.DataContextChangeAttributes == null || property.DataContextChangeAttributes.Length == 0)
            {
                return dataContextType;
            }

            var (childType, extensionParameters, addLayer, serverOnly) = ApplyDataContextChange(dataContextType, property.DataContextChangeAttributes, obj, property);

            if (!addLayer)
            {
                Debug.Assert(childType == dataContextType.DataContextType);
                return DataContextStack.Create(dataContextType.DataContextType, dataContextType.Parent, dataContextType.NamespaceImports, extensionParameters.Concat(dataContextType.ExtensionParameters).ToArray(), dataContextType.BindingPropertyResolvers);
            }

            if (childType is null) return null; // childType is null in case there is some error in processing (e.g. enumerable was expected).
            else return DataContextStack.Create(childType, dataContextType, extensionParameters: extensionParameters.ToArray(), serverSideOnly: serverOnly);
        }

        /// <summary> Return the expected data context type for this property. Returns null if the type is unknown. </summary>
        public static DataContextStack GetDataContextType(this DotvvmProperty property, ResolvedControl obj, DataContextStack? objDataContext = null)
        {
            var dataContextType = objDataContext ?? obj.DataContextTypeStack;

            if (property.DataContextManipulationAttribute != null)
            {
                return (DataContextStack)property.DataContextManipulationAttribute.ChangeStackForChildren(
                    dataContextType, obj, property,
                    (parent, changeType) => DataContextStack.Create(ResolvedTypeDescriptor.ToSystemType(changeType), (DataContextStack)parent));
            }

            if (property.DataContextChangeAttributes == null || property.DataContextChangeAttributes.Length == 0)
            {
                return dataContextType;
            }

            var (childType, extensionParameters, addLayer, serverOnly) = ApplyDataContextChange(dataContextType, property.DataContextChangeAttributes, obj, property);

            if (!addLayer)
            {
                Debug.Assert(childType == dataContextType.DataContextType);
                return DataContextStack.Create(dataContextType.DataContextType, dataContextType.Parent, dataContextType.NamespaceImports, extensionParameters.Concat(dataContextType.ExtensionParameters).ToArray(), dataContextType.BindingPropertyResolvers);
            }

            if (childType is null)
                childType = typeof(UnknownTypeSentinel);

            return DataContextStack.Create(childType, dataContextType, extensionParameters: extensionParameters.ToArray(), serverSideOnly: serverOnly);
        }

        public static (Type? type, List<BindingExtensionParameter> extensionParameters, bool addLayer, bool serverOnly) ApplyDataContextChange(DataContextStack dataContext, DataContextChangeAttribute[] attributes, ResolvedControl control, DotvvmProperty? property)
        {
            var type = ResolvedTypeDescriptor.Create(dataContext.DataContextType);
            var extensionParameters = new List<BindingExtensionParameter>();
            var addLayer = false;
            var serverOnly = dataContext.ServerSideOnly;

            foreach (var attribute in attributes.OrderBy(a => a.Order))
            {
                if (type == null) break;
                extensionParameters.AddRange(attribute.GetExtensionParameters(type));
                if (attribute.NestDataContext)
                {
                    addLayer = true;
                    type = attribute.GetChildDataContextType(type, dataContext, control, property);
                    serverOnly = attribute.IsServerSideOnly(dataContext, control, property) ?? serverOnly;
                }
            }
            return (ResolvedTypeDescriptor.ToSystemType(type), extensionParameters, addLayer, serverOnly);
        }


        private static (Type? childType, List<BindingExtensionParameter> extensionParameters, bool addLayer, bool serverOnly) ApplyDataContextChange(DataContextStack dataContextType, DataContextChangeAttribute[] attributes, DotvvmBindableObject obj, DotvvmProperty property)
        {
            Type? type = dataContextType.DataContextType;
            var extensionParameters = new List<BindingExtensionParameter>();
            var addLayer = false;
            var serverOnly = dataContextType.ServerSideOnly;

            foreach (var attribute in attributes.OrderBy(a => a.Order))
            {
                if (type == null) break;
                extensionParameters.AddRange(attribute.GetExtensionParameters(new ResolvedTypeDescriptor(type)));
                if (attribute.NestDataContext)
                {
                    addLayer = true;
                    type = attribute.GetChildDataContextType(type, dataContextType, obj, property);
                    serverOnly = attribute.IsServerSideOnly(dataContextType, obj, property) ?? serverOnly;
                }
            }

            return (type, extensionParameters, addLayer, serverOnly);
        }

        /// <summary>
        /// Annotates `_this`, `_parent`, `_root` parameters with BindingParameterAnnotation indicating their DataContext
        /// </summary>
        public static Expression AnnotateStandardContextParams(Expression expr, DataContextStack dataContext) =>
            new ParameterAnnotatingVisitor(dataContext).Visit(expr);

        class ParameterAnnotatingVisitor : ExpressionVisitor
        {
            public readonly DataContextStack DataContext;

            public ParameterAnnotatingVisitor(DataContextStack dataContext)
            {
                this.DataContext = dataContext;
            }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.GetParameterAnnotation() != null) return node;

                if (node.Name is null) return base.VisitParameter(node);

                if (node.Name == "_this") return node.AddParameterAnnotation(new BindingParameterAnnotation(DataContext));
                else if (node.Name == "_parent") return node.AddParameterAnnotation(new BindingParameterAnnotation(DataContext.Parent));
                else if (node.Name == "_root") return node.AddParameterAnnotation(new BindingParameterAnnotation(DataContext.EnumerableItems().Last()));
                else if (node.Name.StartsWith("_parent", StringComparison.Ordinal) && int.TryParse(node.Name.Substring("_parent".Length), out int index))
                    return node.AddParameterAnnotation(new BindingParameterAnnotation(DataContext.EnumerableItems().ElementAt(index)));
                return base.VisitParameter(node);
            }
        }

        public static BindingDelegate<T> ToGeneric<T>(this BindingDelegate d) => control => (T)d(control)!;
        public static BindingUpdateDelegate<T> ToGeneric<T>(this BindingUpdateDelegate d) => (control, val) => d(control, val);

        public static string GetBindingName(this IBinding binding) =>
            binding switch {
                ControlPropertyBindingExpression => "controlProperty",
                ValueBindingExpression => "value",
                ResourceBindingExpression => "resource",
                ControlCommandBindingExpression => "controlCommand",
                StaticCommandBindingExpression => "staticCommand",
                CommandBindingExpression => "command",
                _ => binding.GetType().Name
            };

        public record InvalidDataContextTypeException(
            DotvvmBindableObject Control,
            object? ContextObject,
            DataContextStack ControlContext,
            DataContextStack BindingContext,
            Type?[]? ActualContextTypes
        )
            : DotvvmExceptionBase(
                RelatedBinding: ContextObject as IBinding,
                RelatedControl: Control
            )
        {
            public override string Message
            {
                get
                {
                    var message = new StringBuilder()
                        .Append($"Could not find DataContext space of '{ContextObject}'. The DataContextType property of the binding does not correspond to DataContextType of the {Control.GetType().Name} nor any of its ancestors.");

                    var stackComparison = DataContextStack.CompareStacksMessage(ControlContext, BindingContext);

                    for (var i = 0; i < stackComparison.Length; i++)
                    {
                        var level = i switch {
                            0 => "_this:    ",
                            1 => "_parent:  ",
                            _ => $"_parent{i}: "
                        };

                        message.Append($"\nControl {level}");
                        foreach (var (control, binding) in stackComparison[i])
                        {
                            var length = Math.Max(control.Length, binding.Length);
                            if (control == binding)
                                message.Append(control);
                            else
                                message.Append(StringUtils.PadCenter(StringUtils.UnicodeUnderline(control), length + 2));
                        }

                        message.Append($"\nBinding {level}");
                        foreach (var (control, binding) in stackComparison[i])
                        {
                            var length = Math.Max(control.Length, binding.Length);
                            if (control == binding)
                                message.Append(binding);
                            else
                                message.Append(StringUtils.PadCenter(StringUtils.UnicodeUnderline(binding), length + 2));
                        }
                    }

                    if (ActualContextTypes is {})
                        message.Append($"\nReal data context types: {string.Join(", ", ActualContextTypes.Select(t => t?.ToCode(stripNamespace: true) ?? "null"))}");
                    return message.ToString();
                }
            }
        }

        public record BindingNotSupportedException(IBinding Binding, [CallerMemberName] string Caller = "")
            : DotvvmExceptionBase(RelatedBinding: Binding)
        {
            public override string Message => $"Binding {Binding} is not supported in {Caller} method";
        }

        public record InvalidBindingTypeException(IBinding Binding, Type ExpectedType)
            : DotvvmExceptionBase(RelatedBinding: Binding)
        {
            public override string Message => $"The binding result type {Binding.GetProperty<ResultTypeBindingProperty>(ErrorHandlingMode.ReturnNull)?.Type.FullName} is not assignable to {ExpectedType.FullName}";

            public static void CheckType(IBinding binding, Type expectedType)
            {
                if (binding.GetProperty<ResultTypeBindingProperty>(ErrorHandlingMode.ReturnNull) is ResultTypeBindingProperty resultType &&
                        !expectedType.IsAssignableFrom(resultType.Type))
                    throw new InvalidBindingTypeException(binding, expectedType);
            }
        }
    }


    public class BindingParameterAnnotation
    {
        public readonly DataContextStack? DataContext;
        public readonly BindingExtensionParameter? ExtensionParameter;

        public BindingParameterAnnotation(DataContextStack? context = null, BindingExtensionParameter? extensionParameter = null)
        {
            this.DataContext = context;
            this.ExtensionParameter = extensionParameter;
        }
    }
}
