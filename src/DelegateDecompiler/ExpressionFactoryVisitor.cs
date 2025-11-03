using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DelegateDecompiler
{
    class ExpressionFactoryVisitor : ExpressionVisitor
    {
        public static Expression Build(Expression expression)
        {
            var visitor = new ExpressionFactoryVisitor();
            return visitor.Visit(expression.Decompile().Decompile().Optimize());
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.GetCustomAttributes(true).OfType<ExpressionFactoryAttribute>().Any())
            {
                List<object> args = new List<object>();
                for (int i = 0; i < node.Arguments.Count; i++)
                {
                    var argInfos = node.Method.GetParameters()[i];
                    var argType = argInfos.ParameterType;
                    if (!ExpressionFactoryArgumentVisitor.GetValue(argType, node.Arguments[i], out var arg))
                        throw new InvalidOperationException($"Could not convert the parameter {argInfos.Name} from {node.Arguments[i].Type} into {argType.Name} for factory method {node.Method.Name}");
                    args.Add(arg);
                }
                var result = (Expression)node.Method.Invoke(node.Object, args.ToArray());
                return result;
            }
            return base.VisitMethodCall(node);
        }

        class ExpressionFactoryArgumentVisitor : ExpressionVisitor
        {
            private Type argType;
            protected object arg = null;

            public static bool GetValue(Type argType, Expression expr, out object arg)
            {
                var visitor = new ExpressionFactoryArgumentVisitor(argType);
                visitor.Visit(expr);
                arg = visitor.arg;
                return arg != null;
            }

            ExpressionFactoryArgumentVisitor(Type argType)
            {
                this.argType = argType;
            }

            public override Expression Visit(Expression node)
            {
                return base.Visit(node);
            }
            protected override Expression VisitConstant(ConstantExpression node)
            {
                return base.VisitConstant(node);
            }

            protected override Expression VisitMember(MemberExpression memberExpression)
            {
                // Recurse down to see if we can simplify...
                var expression = Visit(memberExpression.Expression);

                // If we've ended up with a constant, and it's a property or a field,
                // we can simplify ourselves to a constant
                if (expression is ConstantExpression)
                {
                    object container = ((ConstantExpression)expression).Value;
                    var member = memberExpression.Member;
                    if (member is FieldInfo)
                    {
                        arg = ((FieldInfo)member).GetValue(container);
                    }
                    if (member is PropertyInfo)
                    {
                        arg = ((PropertyInfo)member).GetValue(container, null);
                    }
                    return memberExpression;
                }
                else if (argType.IsAssignableFrom(memberExpression.Type))
                {
                    arg = Expression.Lambda(memberExpression, null);
                    return memberExpression;
                }
                return base.VisitMember(memberExpression);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name == nameof(Expression.Constant)
                    && node.Method.DeclaringType == typeof(Expression))
                    return base.Visit(node.Arguments.First());
                return base.VisitMethodCall(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (argType.IsAssignableFrom(node.Type))
                {
                    arg = node;
                }
                return node;
            }
            protected override Expression VisitUnary(UnaryExpression node)
            {
                if (node.NodeType == ExpressionType.Quote)
                {
                    arg = node.Operand;
                    return node;
                }
                return base.Visit(node.Operand);
            }
        }
    }
}
