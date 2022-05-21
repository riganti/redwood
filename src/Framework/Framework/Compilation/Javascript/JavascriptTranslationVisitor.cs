using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.HelperNamespace;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Compilation.Javascript.Ast;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Utils;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using FastExpressionCompiler;

namespace DotVVM.Framework.Compilation.Javascript
{
    public class JavascriptTranslationVisitor
    {
        private readonly IJavascriptMethodTranslator Translator;

        public DataContextStack DataContext { get; }

        private readonly Dictionary<DataContextStack, int> ContextMap;
        public bool WriteUnknownParameters { get; set; } = true;
        public JavascriptTranslationVisitor(DataContextStack dataContext, IJavascriptMethodTranslator translator)
        {
            this.ContextMap =
                dataContext
                .EnumerableItems()
                .Where(c => !c.ServerSideOnly) // server-side only data contexts are not present at all client-side, so we need to skip them before assigning indices
                .Select((a, i) => (a, i))
                .ToDictionary(a => a.a, a => a.i);
            this.DataContext = dataContext;
            this.Translator = translator;
        }
        public JsExpression Translate(Expression expression)
        {
            if (expression.GetParameterAnnotation() is BindingParameterAnnotation annotation)
                return TranslateParameter(expression, annotation);

            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    return TranslateConstant((ConstantExpression)expression);

                case ExpressionType.Call:
                    return TranslateMethodCall((MethodCallExpression)expression);

                case ExpressionType.MemberAccess:
                    return TranslateMemberAccess((MemberExpression)expression);

                case ExpressionType.Parameter:
                    return TranslateParameter((ParameterExpression)expression);

                case ExpressionType.Conditional:
                    return TranslateConditional((ConditionalExpression)expression);

                case ExpressionType.Index:
                    return TranslateIndex((IndexExpression)expression);

                case ExpressionType.Assign:
                    return TranslateAssign((BinaryExpression)expression);

                case ExpressionType.Lambda:
                    return TranslateLambda((LambdaExpression)expression);

                case ExpressionType.Block:
                    return TranslateBlock((BlockExpression)expression);

                case ExpressionType.Default:
                    return TranslateDefault((DefaultExpression)expression);

                case ExpressionType.Invoke:
                    return TranslateInvoke((InvocationExpression)expression);

                case ExpressionType.NewArrayInit:
                    return TranslateNewArrayInit((NewArrayExpression)expression);
            }
            if (expression is BinaryExpression)
            {
                return TranslateBinary((BinaryExpression)expression);
            }
            else if (expression is UnaryExpression)
            {
                return TranslateUnary((UnaryExpression)expression);
            }

            throw new NotSupportedException($"The expression type {expression.NodeType} cannot be translated to Javascript!");
        }

        private JsExpression TranslateNewArrayInit(NewArrayExpression expression)
        {
            var innerExpressions = expression.Expressions.Select(Translate).ToList();
            return new JsArrayExpression(innerExpressions);
        }

        private JsExpression TranslateInvoke(InvocationExpression expression)
        {
            // just invoke the function
            return Translate(expression.Expression).Invoke(expression.Arguments.Select(Translate));
        }

        private Expression ReplaceVariables(Expression node, IReadOnlyList<ParameterExpression> variables, CodeSymbolicParameter[] args)
        {
            return ExpressionUtils.Replace(Expression.Lambda(node, variables), args.Zip(variables, (o, a) => Expression.Parameter(a.Type, a.Name).AddParameterAnnotation(
                new BindingParameterAnnotation(extensionParameter: new FakeExtensionParameter(_ => new JsSymbolicParameter(o), a.Name!, new ResolvedTypeDescriptor(a.Type)))
            )).ToArray());
        }

        public JsExpression TranslateLambda(LambdaExpression expression)
        {
            var args = expression.Parameters.Select(p => new CodeSymbolicParameter($"lambda_param_" + p.Name)).ToArray();
            var body = Translate(ReplaceVariables(expression.Body, expression.Parameters, args));
            var usedNames = new HashSet<string>(body.DescendantNodesAndSelf().OfType<JsIdentifierExpression>().Select(i => i.Identifier));
            var argsNames = expression.Parameters.Select(p => JsTemporaryVariableResolver.GetNames(p.Name).First(usedNames.Add)).ToArray();

            var functionExpr = new JsArrowFunctionExpression(
                argsNames.Select(n => new JsIdentifier(n)),
                body
            );

            foreach (var symArg in body.DescendantNodesAndSelf().OfType<JsSymbolicParameter>())
            {
                var aIndex = Array.IndexOf(args, symArg.Symbol);
                if (aIndex >= 0)
                {
                    symArg.ReplaceWith(
                        new JsIdentifierExpression(argsNames[aIndex])
                            .WithAnnotations(symArg.Annotations)
                            .WithAnnotation(ResultMayBeObservableAnnotation.Instance, append: false));
                }
            }

            return functionExpr;
        }

        public JsExpression TranslateBlock(BlockExpression block)
        {
            if (block.Expressions.Count == 0)
                return new JsLiteral(0).Unary(UnaryOperatorType.Void);

            var vars = block.Variables.Select(v => new JsTemporaryVariableParameter(preferredName: v.Name)).ToArray();
            var body = block.Expressions.Select(s => Translate(ReplaceVariables(s, block.Variables, vars))).ToArray();
            if (body.Length == 1) return body[0];
            return body.Aggregate(
                (a, b) => (JsExpression)new JsBinaryExpression(a, BinaryOperatorType.Sequence, b));
        }

        public JsExpression TranslateAssign(BinaryExpression expression)
        {
            var property = expression.Left as MemberExpression;
            if (property != null)
            {
                if (property.Expression is null)
                    throw new NotSupportedException($"Assignment of static property {expression} is not supported.");
                var target = Translate(property.Expression);
                var value = Translate(expression.Right);
                return TryTranslateMethodCall((property.Member as PropertyInfo)?.SetMethod, property.Expression, new[] { expression.Right }) ??
                    SetProperty(target, new VMPropertyInfoAnnotation(property.Member), value);
            }
            else if (expression.Left.GetParameterAnnotation() is BindingParameterAnnotation annotation)
            {
                if (annotation.ExtensionParameter == null) throw new NotSupportedException($"Cannot assign to data context parameter {expression.Left}");
                return new JsAssignmentExpression(
                    TranslateParameter(expression.Left, annotation),
                    Translate(expression.Right)
                );
            }
            throw new NotSupportedException($"Cannot assign expression of type {expression.Left.NodeType}!");
        }

        private JsExpression SetProperty(JsExpression target, VMPropertyInfoAnnotation property, JsExpression value) =>
            new JsAssignmentExpression(TranslateViewModelProperty(target, property), value);

        public JsExpression TranslateConditional(ConditionalExpression expression) =>
            new JsConditionalExpression(
                Translate(expression.Test),
                Translate(expression.IfTrue),
                Translate(expression.IfFalse));

        public JsExpression TranslateIndex(IndexExpression expression, bool setter = false)
        {
            if (expression.Object is null)
                throw new NotSupportedException($"Static indexer {expression} is not supported.");
            if (expression.Indexer is null)
                throw new NotSupportedException($"IndexExpression does not have indexer."); // wtf, but it's nullable
            var target = Translate(expression.Object);
            var args = expression.Arguments.Select(Translate).ToArray();
            var method = setter ? expression.Indexer.SetMethod : expression.Indexer.GetMethod;

            var result = TryTranslateMethodCall(method, expression.Object, expression.Arguments.ToArray());
            if (result != null) return result;
            return BuildIndexer(target, args.Single(), expression.Indexer);
        }

        public static JsExpression BuildIndexer(JsExpression target, JsExpression index, MemberInfo member) =>
            target.Indexer(index).WithAnnotation(new VMPropertyInfoAnnotation(member));

        public JsExpression TranslateParameter(Expression expression, BindingParameterAnnotation annotation)
        {
            if (annotation.DataContext is { ServerSideOnly: true })
                throw new NotSupportedException($"{expression} of type {annotation.DataContext.DataContextType.ToCode(stripNamespace: true)} cannot be translated to JavaScript, it can only be used in resource and command bindings. This is most likely because the data context is bound to a resource binding.");

            JsExpression getDataContext(int parentContexts)
            {
                JsExpression context = new JsSymbolicParameter(JavascriptTranslator.KnockoutContextParameter);
                for (var i = 0; i < parentContexts; i++)
                    context = context.Member("$parentContext");
                return context;
            }
            int getContextSteps(DataContextStack? item) =>
                item == null ? 0 : ContextMap[item];

            if (annotation.ExtensionParameter != null)
            {
                var type = ResolvedTypeDescriptor.ToSystemType(annotation.ExtensionParameter.ParameterType) ?? expression.Type;
                return annotation.ExtensionParameter.GetJsTranslation(getDataContext(getContextSteps(annotation.DataContext)))
                    .WithAnnotation(new ViewModelInfoAnnotation(type, extensionParameter: annotation.ExtensionParameter), append: false);
            }
            else
            {
                var index = getContextSteps(annotation.DataContext);
                var data = JavascriptTranslator.GetKnockoutViewModelParameter(index).ToExpression();
                return data.WithAnnotation(new ViewModelInfoAnnotation(expression.Type));
            }
        }

        public JsExpression TranslateParameter(ParameterExpression expression)
        {
            if (WriteUnknownParameters && !string.IsNullOrEmpty(expression.Name)) return new JsIdentifierExpression(expression.Name);
            else throw new NotSupportedException($"Can't translate parameter '{expression}' to Javascript.");
        }

        public JsLiteral TranslateDefault(DefaultExpression expression) =>
            new JsLiteral(expression.Type.IsValueType && expression.Type != typeof(void) ?
                          Activator.CreateInstance(expression.Type) :
                          null)
            .WithAnnotation(new ViewModelInfoAnnotation(expression.Type, containsObservables: false));

        public static JsLiteral TranslateConstant(ConstantExpression expression) =>
            new JsLiteral(expression.Value).WithAnnotation(new ViewModelInfoAnnotation(expression.Type, containsObservables: false));

        public JsExpression TranslateMethodCall(MethodCallExpression expression)
        {
            var result = TryTranslateMethodCall(expression.Method, expression.Object, expression.Arguments.ToArray());
            if (result == null)
                throw new NotSupportedException($"Method {ReflectionUtils.FormatMethodInfo(expression.Method)} cannot be translated to Javascript");
            return result;
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public JsExpression TranslateBinary(BinaryExpression expression)
        {
            var left = Translate(expression.Left);
            var right = Translate(expression.Right);
            var method = expression.Method;
            if (method != null)
            {
                var mTranslate = TryTranslateMethodCall(expression.Method, null, new[] { expression.Left, expression.Right });
                if (mTranslate != null) return mTranslate;
            }
            BinaryOperatorType op;
            bool mayInduceDecimals = false; // whether the operation can have non-integer result just from integer inputs
            switch (expression.NodeType)
            {
                case ExpressionType.Equal: op = BinaryOperatorType.Equal; break;
                case ExpressionType.NotEqual: op = BinaryOperatorType.NotEqual; break;
                case ExpressionType.AndAlso: op = BinaryOperatorType.ConditionalAnd; break;
                case ExpressionType.OrElse: op = BinaryOperatorType.ConditionalOr; break;
                case ExpressionType.GreaterThan: op = BinaryOperatorType.Greater; break;
                case ExpressionType.LessThan: op = BinaryOperatorType.Less; break;
                case ExpressionType.GreaterThanOrEqual: op = BinaryOperatorType.GreaterOrEqual; break;
                case ExpressionType.LessThanOrEqual: op = BinaryOperatorType.LessOrEqual; break;
                case ExpressionType.AddChecked:
                case ExpressionType.Add: op = BinaryOperatorType.Plus; break;
                case ExpressionType.SubtractChecked:
                case ExpressionType.Subtract: op = BinaryOperatorType.Minus; break;
                case ExpressionType.SubtractAssignChecked:
                case ExpressionType.Divide: op = BinaryOperatorType.Divide; mayInduceDecimals = true; break;
                case ExpressionType.Modulo: op = BinaryOperatorType.Modulo; break;
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Multiply: op = BinaryOperatorType.Times; break;
                case ExpressionType.LeftShift: op = BinaryOperatorType.LeftShift; break;
                case ExpressionType.RightShift: op = BinaryOperatorType.UnsignedRightShift; break;
                case ExpressionType.And: op = BinaryOperatorType.BitwiseAnd; break;
                case ExpressionType.Or: op = BinaryOperatorType.BitwiseOr; break;
                case ExpressionType.ExclusiveOr: op = BinaryOperatorType.BitwiseXOr; break;
                case ExpressionType.Coalesce: op = BinaryOperatorType.NullishCoalescing; break;
                case ExpressionType.ArrayIndex: return new JsIndexerExpression(left, right).WithAnnotation(new VMPropertyInfoAnnotation(null, expression.Type));
                default:
                    throw new NotSupportedException($"Unary operator of type { expression.NodeType } is not supported");
            }
            var result = new JsBinaryExpression(left, op, right);
            if (mayInduceDecimals && ReflectionUtils.IsNumericType(expression.Type) && expression.Type != typeof(float) && expression.Type != typeof(double) && expression.Type != typeof(decimal))
            {
                if (new[] { typeof(Byte), typeof(SByte), typeof(Int16), typeof(UInt16), typeof(Int32) }.Contains(expression.Type))
                    // `(expr | 0)` to get the integer result type
                    return new JsBinaryExpression(
                        result,
                        BinaryOperatorType.BitwiseOr,
                        new JsLiteral(0));
                else if (new[] { typeof(UInt32), typeof(UInt64) }.Contains(expression.Type))
                    // round down, unsigned integers are always rounded down
                    return new JsIdentifierExpression("Math").Member("floor").Invoke(result);
                else
                    // round to zero, by trimming a string...
                    return new JsIdentifierExpression("parseInt").Invoke(result);
            }
            if (expression.NodeType == ExpressionType.Add && expression.Type == typeof(string))
            {
                // when adding strings in JS `"a" + null` will equal to `"anull"` while in C# it equals to `"a"`.
                // we need to replace null values with an empty string to avoid the difference:

                ReplaceNullValue(result.Left, expression.Left, new JsLiteral(""));
                ReplaceNullValue(result.Right, expression.Right, new JsLiteral(""));
            }
            if (expression.NodeType == ExpressionType.ExclusiveOr && expression.Left.Type == typeof(bool) && expression.Right.Type == typeof(bool))
            {
                // Whenever operator ^ is applied on two booleans in .NET, the result is also boolean

                return new JsBinaryExpression(left.Clone(), BinaryOperatorType.NotEqual, right.Clone());
            }
            
            return result;
        }

        private void ReplaceNullValue(JsExpression js, Expression expr, JsExpression replacement)
        {
            if (!PrimitiveToStringTranslator.CanBeNull(expr))
            {
            }
            else if (js is JsLiteral literal)
            {
                if (literal.Value is null)
                    js.ReplaceWith(replacement);
            }
            // avoid doing it if the result can't be null

            // these JS expressions can't be null
            else if (js is JsBinaryExpression or JsUnaryExpression or JsBaseFunctionExpression or JsObjectExpression or JsNewExpression)
            {
            }
            // view model is never null
            else if (js is JsSymbolicParameter symbolicParam && symbolicParam.Symbol is JavascriptTranslator.ContextSymbolicParameter or JavascriptTranslator.ViewModelSymbolicParameter)
            {
            }
            // dotvvm.globalize.bindingNumberToString(), dotvvm.globalize.format(), String() never return null
            else if (js is JsInvocationExpression { Target: JsMemberAccessExpression { MemberName: "bindingNumberToString", Target: JsMemberAccessExpression { MemberName: "globalize", Target: JsIdentifierExpression { Identifier: "dotvvm" } } } }
                        or JsInvocationExpression { Target: JsMemberAccessExpression { MemberName: "format", Target: JsMemberAccessExpression { MemberName: "globalize", Target: JsIdentifierExpression { Identifier: "dotvvm" } } } }
                        or JsInvocationExpression { Target: JsIdentifierExpression { Identifier: "String" } })
            {
            }
            else
            {
                js.ReplaceWith(x => new JsBinaryExpression(x, BinaryOperatorType.NullishCoalescing, replacement));
            }
        }

        public JsExpression TranslateUnary(UnaryExpression expression)
        {
            var method = expression.Method;
            if (method != null)
            {
                var mTranslate = TryTranslateMethodCall(expression.Method, null, new[] { expression.Operand });
                if (mTranslate != null) return mTranslate;
            }
            var operand = Translate(expression.Operand);
            UnaryOperatorType op;
            switch (expression.NodeType)
            {
                case ExpressionType.NegateChecked:
                case ExpressionType.Negate:
                    op = UnaryOperatorType.Minus;
                    break;

                case ExpressionType.UnaryPlus:
                    op = UnaryOperatorType.Plus;
                    break;

                case ExpressionType.Not:
                    if (expression.Operand.Type == typeof(bool))
                        op = UnaryOperatorType.LogicalNot;
                    else op = UnaryOperatorType.BitwiseNot;
                    break;

                case ExpressionType.OnesComplement:
                    op = UnaryOperatorType.BitwiseNot;
                    break;

                case ExpressionType.Convert:
                case ExpressionType.TypeAs:
                    // convert does not make sense in Javascript
                    return TranslateConvert(expression.Operand, operand, expression.Type);

                default:
                    throw new NotSupportedException($"Unary operator of type { expression.NodeType } is not supported");
            }
            return new JsUnaryExpression(op, operand);
        }

        public JsExpression TranslateConvert(Expression originalOperand, JsExpression operand, Type target)
        {
            // float -> integer: we just apply x | 0
            if (originalOperand.Type.IsRealNumericType() && target.IsIntegerNumericType())
                return new JsBinaryExpression(operand, BinaryOperatorType.BitwiseOr, new JsLiteral(0));

            // int -> enum: use dotvvm.translations.enums.fromInt
            if (originalOperand.Type.IsIntegerNumericType() && target.IsEnum)
            {
                // shortcut for constant integers (it's used by C# compiler when inserting constant enums)
                if (originalOperand.NodeType == ExpressionType.Constant)
                {
                    var enumValue = Enum.ToObject(target, ((ConstantExpression)originalOperand).Value!);
                    // JsLiteral will JSON-serialize the enumValue
                    return new JsLiteral(enumValue);
                }

                return new JsIdentifierExpression("dotvvm")
                    .Member("translations").Member("enums").Member("fromInt")
                    .Invoke(operand, new JsLiteral(target.GetTypeHash()));
            }

            // enum -> int: use dotvvm.translations.enums.toInt
            if (originalOperand.Type.IsEnum && target.IsIntegerNumericType())
            {
                // shortcut for constant integers (it's used by the TranslateBinary method)
                if (operand is JsLiteral { Value: Enum value })
                {
                    return new JsLiteral(Convert.ToInt32(value));
                }
                return new JsIdentifierExpression("dotvvm")
                    .Member("translations").Member("enums").Member("toInt")
                    .Invoke(operand, new JsLiteral(originalOperand.Type.GetTypeHash()));
            }

            // by default, just allow any conversion
            return operand;
        }

        public JsExpression TranslateMemberAccess(MemberExpression expression)
        {
            var getter = (expression.Member as PropertyInfo)?.GetMethod;
            if (expression.Expression == null)
            {
                // static
                return TryTranslateMethodCall(getter, null, new Expression[0]) ??
                    new JsLiteral((
                        ((expression.Member as FieldInfo)?.GetValue(null) ?? (expression.Member as PropertyInfo)?.GetValue(null))));
            }
            else
            {
                return TryTranslateMethodCall(getter, expression.Expression, new Expression[0]) ??
                    TranslateViewModelProperty(Translate(expression.Expression), new VMPropertyInfoAnnotation(expression.Member));
            }
        }

        public static JsExpression TranslateViewModelProperty(JsExpression context, VMPropertyInfoAnnotation propInfo, string? name = null) =>
            new JsMemberAccessExpression(context, name ?? propInfo.MemberInfo.NotNull().Name)
                .WithAnnotation(propInfo)
                .WithAnnotation(new ViewModelInfoAnnotation(propInfo.ResultType));

        public JsExpression? TryTranslateMethodCall(MethodInfo? methodInfo, Expression? target, IEnumerable<Expression> arguments) =>
            methodInfo is null ? null :
            Translator.TryTranslateCall(
                target is null ? null : new LazyTranslatedExpression(target, Translate),
                arguments.Select(a => new LazyTranslatedExpression(a, Translate)).ToArray(),
                methodInfo)
                ?.WithAnnotation(new ViewModelInfoAnnotation(methodInfo.ReturnType), append: false);

        public class FakeExtensionParameter : BindingExtensionParameter
        {
            private readonly Func<JsExpression, JsExpression> getJsTranslation;

            public FakeExtensionParameter(Func<JsExpression, JsExpression> getJsTranslation, string identifier = "__", ITypeDescriptor? type = null, bool inherit = false) : base(identifier, type, inherit)
            {
                this.getJsTranslation = getJsTranslation;
            }

            public override Expression GetServerEquivalent(Expression controlParameter) => throw new NotSupportedException();
            public override JsExpression GetJsTranslation(JsExpression dataContext) => getJsTranslation(dataContext);
        }
    }

    /// Represents an Linq.Expression that is being translated to JsAst.
    public class LazyTranslatedExpression
    {
        private readonly Lazy<JsExpression> lazyJsExpression;
        public JsExpression JsExpression() => lazyJsExpression.Value;
        public Expression OriginalExpression { get; }
        public LazyTranslatedExpression(Expression expr, Func<Expression, JsExpression> translateMethod)
        {
            this.OriginalExpression = expr ?? throw new ArgumentNullException(nameof(expr));
            this.lazyJsExpression = new(() => translateMethod(expr));
        }
    }
}
