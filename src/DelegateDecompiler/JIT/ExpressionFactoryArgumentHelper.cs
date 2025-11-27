using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace DelegateDecompiler.JIT
{
    internal static class ExpressionFactoryArgumentHelper
    {
        public static bool TryExtractArgument(Type expectedType, Expression expr, out object arg)
        {
            arg = null;
            if (expr == null) return false;

            // If it's already a ConstantExpression holding an Expression (e.g. Expression.Constant(lambda))
            if (expr is ConstantExpression constExpr)
            {
                // If expected is Expression and constant holds an Expression, return it
                if (constExpr.Value is Expression innerExpr && (typeof(Expression).IsAssignableFrom(expectedType) || expectedType == typeof(Expression)))
                {
                    // If a specific Expression<T> is required, try to convert
                    if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>) && innerExpr is LambdaExpression lambda)
                    {
                        try
                        {
                            var typedLambdaFromConst = Expression.Lambda(expectedType, lambda.Body, lambda.Parameters.ToArray());
                            arg = typedLambdaFromConst;
                            return true;
                        }
                        catch
                        {
                            // fall through to returning raw
                        }
                    }

                    arg = innerExpr;
                    return true;
                }

                // otherwise return the constant value if convertible
                if (constExpr.Value != null && expectedType.IsInstanceOfType(constExpr.Value))
                {
                    arg = constExpr.Value;
                    return true;
                }
            }

            // Lambda expression -> try to produce the exact Expression<> type requested
            if (expr is LambdaExpression lambdaExpr)
            {
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>))
                {
                    try
                    {
                        var typedFromLambda = Expression.Lambda(expectedType, lambdaExpr.Body, lambdaExpr.Parameters.ToArray());
                        arg = typedFromLambda;
                        return true;
                    }
                    catch
                    {
                        // fall back to returning as-is
                    }
                }

                arg = lambdaExpr;
                return true;
            }

            // Quoted lambda -> unwrap
            if (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Quote || unary.NodeType == ExpressionType.Convert))
            {
                var operand = unary.Operand;
                if (operand is LambdaExpression operandLambda && expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>))
                {
                    try
                    {
                        var typedFromQuoted = Expression.Lambda(expectedType, operandLambda.Body, operandLambda.Parameters.ToArray());
                        arg = typedFromQuoted;
                        return true;
                    }
                    catch
                    {
                        // fall through
                    }
                }

                arg = operand;
                return true;
            }

            // New array initializer -> try to evaluate into a concrete array or list if expected type is enumerable/array
            if (expr is NewArrayExpression newArray)
            {
                if (IsExpectedEnumerableOrArray(expectedType, out var elemType))
                {
                    // try to compile only when elements are constants or safe
                    if (!ContainsParameters(newArray))
                    {
                        try
                        {
                            var lambdaForNewArray = Expression.Lambda(Expression.Convert(newArray, typeof(object)));
                            var compiledNewArray = (Func<object>)lambdaForNewArray.Compile();
                            var value = compiledNewArray();
                            if (value != null && (expectedType.IsInstanceOfType(value) || (expectedType.IsArray && value is IEnumerable)))
                            {
                                arg = value;
                                return true;
                            }
                        }
                        catch
                        {
                            // fallthrough
                        }
                    }
                }
            }

            // Method call that produces a list/array (e.g. Enumerable.ToList, constructor calls) - try to evaluate
            if (expr is MethodCallExpression mcall)
            {
                // If factory expects an Expression, don't evaluate
                if (typeof(Expression).IsAssignableFrom(expectedType) || typeof(Delegate).IsAssignableFrom(expectedType))
                    return TryHandleExpressionReturningMethodCall(expectedType, mcall, out arg);

                if (IsExpectedEnumerableOrArray(expectedType, out var elemType2))
                {
                    if (!ContainsParameters(mcall))
                    {
                        try
                        {
                            var lambdaForMethodCall = Expression.Lambda(Expression.Convert(mcall, typeof(object)));
                            var compiledMethodCall = (Func<object>)lambdaForMethodCall.Compile();
                            var value = compiledMethodCall();
                            if (value != null && (expectedType.IsInstanceOfType(value) || (expectedType.IsArray && value is IEnumerable)))
                            {
                                arg = value;
                                return true;
                            }
                        }
                        catch
                        {
                            // fallthrough to other strategies
                        }
                    }
                }

                // Special-case Expression.Constant(...) wrapper calls
                if (mcall.Method == typeof(Expression).GetMethod(nameof(Expression.Constant), new Type[] { typeof(object) }))
                {
                    if (mcall.Arguments.Count > 0)
                        return TryExtractArgument(expectedType, mcall.Arguments[0], out arg);
                }
            }

            // MemberExpression - try to read from constant container or fall back to providing expression
            if (expr is MemberExpression member)
            {
                // If the member is backed by a captured constant, read it
                if (member.Expression is ConstantExpression containerConst)
                {
                    var container = containerConst.Value;
                    var memberInfo = member.Member;
                    if (memberInfo is FieldInfo fi)
                    {
                        var val = fi.GetValue(container);
                        if (val != null && expectedType.IsInstanceOfType(val))
                        {
                            arg = val;
                            return true;
                        }
                        arg = val;
                        return true;
                    }
                    if (memberInfo is PropertyInfo pi)
                    {
                        var val = pi.GetValue(container, null);
                        if (val != null && expectedType.IsInstanceOfType(val))
                        {
                            arg = val;
                            return true;
                        }
                        arg = val;
                        return true;
                    }
                }

                // If factory expects an Expression<> or Delegate, try to create a typed lambda when possible
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>))
                {
                    // Try to infer parameters from the member's root if it's a ParameterExpression
                    var root = GetRootExpression(member);
                    if (root is ParameterExpression param)
                    {
                        try
                        {
                            var typedLambda = Expression.Lambda(expectedType, member, param);
                            arg = typedLambda;
                            return true;
                        }
                        catch
                        {
                            // fallthrough
                        }
                    }
                }

                // As a last resort, attempt to evaluate the member expression by compiling it
                if (!ContainsParameters(member))
                {
                    try
                    {
                        var lambdaForMember = Expression.Lambda(Expression.Convert(member, typeof(object)));
                        var compiledMember = (Func<object>)lambdaForMember.Compile();
                        arg = compiledMember();
                        return true;
                    }
                    catch
                    {
                        // fallthrough
                    }
                }
            }

            // Parameter expression - if expected type accepts it, pass the ParameterExpression
            if (expr is ParameterExpression paramExpr)
            {
                if (expectedType.IsAssignableFrom(paramExpr.Type))
                {
                    arg = paramExpr;
                    return true;
                }

                // If expecting Expression<...> and parameter is the lambda parameter, try to create a lambda with it as body
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>))
                {
                    try
                    {
                        var typedParamLambda = Expression.Lambda(expectedType, paramExpr, Enumerable.Empty<ParameterExpression>());
                        arg = typedParamLambda;
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            // As a final attempt, try to evaluate any expression by compiling it to an object when factory expects a runtime value
            if (!typeof(Expression).IsAssignableFrom(expectedType) && !typeof(Delegate).IsAssignableFrom(expectedType))
            {
                if (!ContainsParameters(expr))
                {
                    try
                    {
                        var lambdaForEval = Expression.Lambda(Expression.Convert(expr, typeof(object)));
                        var compiledEval = (Func<object>)lambdaForEval.Compile();
                        arg = compiledEval();
                        return true;
                    }
                    catch
                    {
                        // If compilation fails, fall through to expression fallback
                    }
                }
            }

            // If the factory parameter expects an Expression or Delegate, return the expression node itself
            if (typeof(Expression).IsAssignableFrom(expectedType) || typeof(Delegate).IsAssignableFrom(expectedType))
            {
                // If expecting a specific Expression<T>, try to coerce
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>) && expr is LambdaExpression le)
                {
                    try
                    {
                        var typedFromLe = Expression.Lambda(expectedType, le.Body, le.Parameters.ToArray());
                        arg = typedFromLe;
                        return true;
                    }
                    catch
                    {
                        // fallthrough
                    }
                }

                arg = expr;
                return true;
            }

            return false;
        }

        static bool IsExpectedEnumerableOrArray(Type expectedType, out Type elementType)
        {
            elementType = null;
            if (expectedType.IsArray)
            {
                elementType = expectedType.GetElementType();
                return true;
            }

            var ienum = expectedType.GetInterfaces().Concat(new[] { expectedType })
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>));
            if (ienum != null)
            {
                elementType = ienum.GetGenericArguments()[0];
                return true;
            }

            return false;
        }

        static bool TryHandleExpressionReturningMethodCall(Type expectedType, MethodCallExpression mcall, out object arg)
        {
            arg = null;
            // If the method call produces an Expression (like Expression.Lambda(...) or Expression.Constant(Expression)) try to unwrap
            if (mcall.Method.DeclaringType == typeof(Expression))
            {
                // Expression.Lambda or Expression.Constant
                if (mcall.Method.Name == nameof(Expression.Lambda) && mcall.Arguments.Count > 0)
                {
                    var first = mcall.Arguments[0];

                    // Unwrap common expression wrappers (Quote, Convert, Expression.Constant, Member->Constant) to reach the underlying node
                    Expression candidate = first;
                    while (true)
                    {
                        if (candidate is UnaryExpression u && (u.NodeType == ExpressionType.Quote || u.NodeType == ExpressionType.Convert))
                        {
                            candidate = u.Operand;
                            continue;
                        }

                        if (candidate is MethodCallExpression mc && mc.Method == typeof(Expression).GetMethod(nameof(Expression.Constant), new Type[] { typeof(object) }) && mc.Arguments.Count > 0)
                        {
                            candidate = mc.Arguments[0];
                            continue;
                        }

                        if (candidate is MemberExpression mem && mem.Expression is ConstantExpression containerConst)
                        {
                            // evaluate captured member to constant
                            try
                            {
                                var container = containerConst.Value;
                                object val = null;
                                if (mem.Member is FieldInfo fi) val = fi.GetValue(container);
                                else if (mem.Member is PropertyInfo pi) val = pi.GetValue(container, null);
                                if (val is Expression vExpr)
                                {
                                    candidate = Expression.Constant(vExpr);
                                    // loop will then unwrap constant
                                    continue;
                                }
                                // otherwise stop unwrapping
                            }
                            catch
                            {
                                // ignore and stop unwrapping
                            }
                        }

                        break;
                    }

                    // If candidate ended up being a ConstantExpression that holds an Expression, return that
                    if (candidate is ConstantExpression c && c.Value is Expression constInner)
                    {
                        if (expectedType.IsInstanceOfType(constInner) || typeof(Expression).IsAssignableFrom(expectedType))
                        {
                            arg = constInner;
                            return true;
                        }
                    }

                    // Try extraction on the unwrapped candidate
                    if (TryExtractArgument(expectedType, candidate, out arg))
                        return true;

                    // Fallback: try to compile the MethodCallExpression to obtain the resulting Expression instance
                    if (!ContainsParameters(mcall))
                    {
                        try
                        {
                            var lambdaForExpr = Expression.Lambda(Expression.Convert(mcall, typeof(object)));
                            var compiled = (Func<object>)lambdaForExpr.Compile();
                            var value = compiled();
                            if (value is Expression exprValue)
                            {
                                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>) && exprValue is LambdaExpression le)
                                {
                                    try
                                    {
                                        var typedFromLe = Expression.Lambda(expectedType, le.Body, le.Parameters.ToArray());
                                        arg = typedFromLe;
                                        return true;
                                    }
                                    catch
                                    {
                                        // fallthrough to returning raw expression
                                    }
                                }

                                if (expectedType.IsInstanceOfType(exprValue) || typeof(Expression).IsAssignableFrom(expectedType))
                                {
                                    arg = exprValue;
                                    return true;
                                }

                                arg = exprValue;
                                return true;
                            }
                        }
                        catch
                        {
                            // ignore compilation errors and fall through to default false
                        }
                    }

                    return false;
                }

                if (mcall.Method.Name == nameof(Expression.Constant) && mcall.Arguments.Count > 0)
                {
                    return TryExtractArgument(expectedType, mcall.Arguments[0], out arg);
                }

                // For other Expression.* factory methods (Field, Property, Call, New, MakeMemberAccess, etc.)
                // try to compile the MethodCallExpression to obtain the resulting Expression instance.
                // This covers cases like Expression.Field(...), Expression.Property(...), Expression.Call(... returning an Expression), etc.
                if (!ContainsParameters(mcall))
                {
                    try
                    {
                        var lambdaForExpr = Expression.Lambda(Expression.Convert(mcall, typeof(object)));
                        var compiled = (Func<object>)lambdaForExpr.Compile();
                        var value = compiled();
                        if (value is Expression exprValue)
                        {
                            // If a specific Expression<T> is requested, try to coerce from LambdaExpression
                            if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Expression<>) && exprValue is LambdaExpression le)
                            {
                                try
                                {
                                    var typedFromLe = Expression.Lambda(expectedType, le.Body, le.Parameters.ToArray());
                                    arg = typedFromLe;
                                    return true;
                                }
                                catch
                                {
                                    // fallthrough to returning raw expression
                                }
                            }

                            if (expectedType.IsInstanceOfType(exprValue) || typeof(Expression).IsAssignableFrom(expectedType))
                            {
                                arg = exprValue;
                                return true;
                            }

                            // Fallback: return the extracted Expression object even if it does not strictly match expectedType
                            arg = exprValue;
                            return true;
                        }
                    }
                    catch
                    {
                        // ignore compilation errors and fall through to default false
                    }
                }
            }

            return false;
        }

        static Expression GetRootExpression(Expression expr)
        {
            while (true)
            {
                if (expr is MemberExpression m && m.Expression != null)
                {
                    expr = m.Expression;
                    continue;
                }
                return expr;
            }
        }

        // Detect whether an expression tree contains any ParameterExpression nodes
        static bool ContainsParameters(Expression expr)
        {
            if (expr == null) return false;
            var detector = new ParameterDetector();
            detector.Visit(expr);
            return detector.Found;
        }

        class ParameterDetector : ExpressionVisitor
        {
            public bool Found { get; private set; }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                Found = true;
                return base.VisitParameter(node);
            }
        }
    }
}
