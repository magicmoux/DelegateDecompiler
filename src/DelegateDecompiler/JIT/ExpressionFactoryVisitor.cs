using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DelegateDecompiler.JIT
{
    public class ExpressionFactoryVisitor : ExpressionVisitor
    {
        public static Expression Build(Expression expression)
        {
            var visitor = new ExpressionFactoryVisitor();
            // Ensure expression is decompiled and optimized before visiting factories
            return visitor.Visit(expression.Decompile()).Decompile().Optimize();
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var isFactory = node.Method.IsDefined(typeof(DelegateDecompiler.ExpressionFactoryAttribute), true);
            if (isFactory)
            {
                var parameters = node.Method.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < node.Arguments.Count; i++)
                {
                    var expected = parameters[i].ParameterType;
                    if (!ExpressionFactoryArgumentHelper.TryExtractArgument(expected, node.Arguments[i], out var arg))
                    {
                        return base.VisitMethodCall(node);
                    }

                    args[i] = arg;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    var expected = parameters[i].ParameterType;
                    var val = args[i];
                    if (val == null || expected.IsInstanceOfType(val)) continue;
                    var converted = ExpressionFactoryInvoker.TryCreateInstance(expected, val);
                    if (converted != null && expected.IsInstanceOfType(converted))
                        args[i] = converted;
                }

                object instanceObj = null;
                if (!node.Method.IsStatic)
                {
                    if (node.Object is ConstantExpression constObj)
                        instanceObj = constObj.Value;
                    else
                        return base.VisitMethodCall(node);
                }

                // Use centralized invoker helper
                if (ExpressionFactoryInvoker.TryInvokeFactory(node.Method, instanceObj, args, out var built))
                {
                    return built;
                }

                return base.VisitMethodCall(node);
            }
            return base.VisitMethodCall(node);
        }
    }
}
