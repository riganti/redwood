using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DotVVM.Framework.Utils;

namespace DotVVM.Framework.Compilation.Binding
{
    /// <summary> Checks that assigned properties have setters without the <see cref="IsExternalInit" /> attribute.
    ///           <paramref name="staticError"/> specifies if the error should be thrown immediately, or only when actually executed at runtime. </summary>
    public class InitOnlyPropertyCheckingVisitor(bool staticError = true): ExpressionVisitor
    {
        public static InitOnlyPropertyCheckingVisitor Instance { get; } = new InitOnlyPropertyCheckingVisitor(true);
        public static InitOnlyPropertyCheckingVisitor InstanceDynamicError { get; } = new InitOnlyPropertyCheckingVisitor(false);

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node is { NodeType: ExpressionType.Assign, Left: MemberExpression { Member: PropertyInfo assignedProperty } } &&
                assignedProperty.IsInitOnly())
            {
                var message = $"Property '{assignedProperty.DeclaringType!.Name}.{assignedProperty.Name}' is init-only and cannot be assigned to in bindings executed server-side. You can only assign to such properties in staticCommand bindings executed on the client.";
                if (staticError)
                {
                    throw new Exception(message);
                }
                else
                {
                    return Expression.Throw(Expression.New(typeof(Exception).GetConstructor([typeof(string)]), [Expression.Constant(message)]));
                }
            }

            return base.VisitBinary(node);
        }
    }
}
