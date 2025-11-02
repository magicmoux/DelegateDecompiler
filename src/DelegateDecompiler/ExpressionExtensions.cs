using System;
using System.Linq.Expressions;

namespace DelegateDecompiler
{
    public static class ExpressionExtensions
    {
        public static Expression Expand(this Expression expression)
        {
            // First decompile the expression to ensure we are running in the DelegateDecompiler pipeline
            // Then expand Expression factories calls
            // Then ensure that any newly created expressions are also decompiled and optimized
            return ExpressionFactoryVisitor.Build(expression.Decompile()).Decompile().Optimize();
        }

        internal static Expression Decompile(this Expression expression)
        {
            return DecompileExpressionVisitor.Decompile(expression);
        }

        internal static Expression Optimize(this Expression expression)
        {
            return OptimizeExpressionVisitor.Optimize(expression);
        }

        internal static T Evaluate<T>(this Expression expression)
        {
            var func = Expression.Lambda<Func<T>>(expression).Compile();
            return func.Invoke();
        }
    }
}
