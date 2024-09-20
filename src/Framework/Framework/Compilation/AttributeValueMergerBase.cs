using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Utils;
using System.Reflection;
using System.Dynamic;
using Microsoft.CSharp.RuntimeBinder;
using DotVVM.Framework.Compilation.Binding;
using System.Linq.Expressions;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using System.Diagnostics.CodeAnalysis;

namespace DotVVM.Framework.Compilation
{
    /// <summary>
    /// Merges provided values based on implemented static 'MergeValues' or 'MergeExpression' method:
    ///
    /// implement public static object MergeValues([DotvvmPropertyId], ValueA, ValueB) and this will decide which will should be used
    /// or implement public static Expression MergeExpressions(DotvvmProperty, Expression a, Expression b)
    /// </summary>
    public abstract class AttributeValueMergerBase : IAttributeValueMerger
    {
        const string MergeValuesMethodName = "MergeValues";
        const string MergeExpressionsMethodName = "MergeExpressions";

        public virtual ResolvedPropertySetter? MergeResolvedValues(ResolvedPropertySetter a, ResolvedPropertySetter b, out string? error)
        {
            var property = a.Property;

            error = null;

            if (a is ResolvedPropertyControlCollection firstCollection && b is ResolvedPropertyControlCollection secondCollection)
            {
                return new ResolvedPropertyControlCollection(property, Enumerable.Concat(firstCollection.Controls, secondCollection.Controls).ToList());
            }

            if (a is ResolvedPropertyTemplate firstTemplate && b is ResolvedPropertyTemplate secondTemplate)
            {
                return new ResolvedPropertyTemplate(property, Enumerable.Concat(firstTemplate.Content, secondTemplate.Content).ToList());
            }

            ResolvedBinding? bindingA;
            var valA = GetExpression(a, out bindingA);
            ResolvedBinding? bindingB;
            var valB = GetExpression(b, out bindingB);

            if (valA == null) { error = $"Could not merge with property type '{a.GetType().Name}"; return null; }
            if (valB == null) { error = $"Could not merge with property type '{b.GetType().Name}"; return null; }

            if (bindingA != null && !typeof(IStaticValueBinding).IsAssignableFrom(bindingA.BindingType) ||
                bindingB != null && !typeof(IStaticValueBinding).IsAssignableFrom(bindingB.BindingType)) { error = $"Cannot merge values of non-value bindings."; return null; }

            if (bindingA != null && bindingB != null)
            {
                if (bindingA.BindingType != bindingB.BindingType) { error = $"Cannot merge values of different binding types"; return null; }
            }

            var resultExpression = TryOptimizeMethodCall(
                TryFindMethod(GetType(), MergeExpressionsMethodName, Expression.Constant(property), Expression.Constant(valA), Expression.Constant(valB)) ??
                TryFindMethod(GetType(), MergeExpressionsMethodName, Expression.Constant(property.Id), Expression.Constant(valA), Expression.Constant(valB))
            ) as Expression;

            // Try to find MergeValues method if MergeExpression does not exists, or try to eval it to constant if expression is not constant
            if (resultExpression == null || valA.NodeType == ExpressionType.Constant && valB.NodeType == ExpressionType.Constant && resultExpression.NodeType != ExpressionType.Constant)
            {
                var methodCall = TryFindMergeMethod(property, valA, valB);
                if (methodCall == null) { error = $"Could not find merge method for '{valA}' and '{valB}'."; return null; }

                var optimizedCall = TryOptimizeMethodCall(methodCall);
                if (optimizedCall != null) resultExpression = Expression.Constant(optimizedCall);
                else if (resultExpression == null) resultExpression = methodCall;
            }

            if (resultExpression.NodeType == ExpressionType.Constant)
            {
                return EmitConstant(resultExpression.CastTo<ConstantExpression>().Value, property, ref error);
            }
            else
            {
                return EmitBinding(resultExpression, property, bindingA ?? bindingB!, ref error);
            }
        }

        protected virtual ResolvedPropertySetter EmitConstant(object? value, DotvvmProperty property, ref string? error)
        {
            return new ResolvedPropertyValue(property, value);
        }

        protected virtual ResolvedPropertySetter? EmitBinding(Expression expression, DotvvmProperty property, ResolvedBinding originalBinding, ref string? error)
        {
            if (originalBinding == null) { error = $"Could not merge constant values to binding '{expression}'."; return null; }
            return new ResolvedPropertyBinding(property,
                originalBinding.WithDifferentExpression(expression, property));
        }

        protected virtual Expression? GetExpression(ResolvedPropertySetter a, out ResolvedBinding? binding)
        {
            binding = null;
            if (a is ResolvedPropertyValue)
            {
                return Expression.Constant(a.CastTo<ResolvedPropertyValue>().Value);
            }
            else if (a is ResolvedPropertyBinding)
            {
                binding = a.CastTo<ResolvedPropertyBinding>().Binding;
                return binding.GetExpression();
            }
            else return null;
        }

        protected virtual object? TryOptimizeMethodCall(MethodCallExpression? methodCall)
        {
            if (methodCall != null && methodCall.Arguments.All(a => a.NodeType == ExpressionType.Constant) && (methodCall.Object == null || methodCall.Object.NodeType == ExpressionType.Constant))
                return methodCall.Method.Invoke(methodCall.Object.CastTo<ConstantExpression>()?.Value,
                    methodCall.Arguments.Select(a => a.CastTo<ConstantExpression>().Value).ToArray());
            return null;
        }

        protected virtual MethodCallExpression? TryFindMergeMethod(DotvvmProperty property, Expression a, Expression b)
        {
            return
                TryFindMethod(GetType(), MergeValuesMethodName, Expression.Constant(property.Id), a, b) ??
                TryFindMethod(GetType(), MergeValuesMethodName, Expression.Constant(property), a, b) ??
                TryFindMethod(GetType(), MergeValuesMethodName, a, b);
        }

        private static MethodCallExpression? TryFindMethod(Type context, string name, params Expression[] parameters)
        {
            var binder = (DynamicMetaObjectBinder)Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(
                CSharpBinderFlags.None, name, null, context,
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType | CSharpArgumentInfoFlags.IsStaticType, null) }
                .Concat(ExpressionHelper.GetBinderArguments(parameters.Length)));
            var result = binder.Bind(DynamicMetaObject.Create(context, Expression.Constant(context)), parameters.Select(e => DynamicMetaObject.Create(null!, e)).ToArray());
            if (result.Expression.NodeType == ExpressionType.Throw) return null;
            Expression expr = result.Expression;
            if (expr.NodeType == ExpressionType.Convert)
            {
                expr = expr.CastTo<UnaryExpression>().Operand;
            }
            var methodCall = expr as MethodCallExpression;
            if (methodCall != null && methodCall.Arguments.SequenceEqual(parameters))
                return methodCall;
            else return null;
        }
        public virtual object? MergePlainValues(DotvvmPropertyId prop, object? a, object? b)
        {
            return ((dynamic)this).MergeValues(prop, a, b);
        }
    }
}
