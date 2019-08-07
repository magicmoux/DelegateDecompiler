using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DelegateDecompiler
{
    public static class DecompileExtensions
    {
        internal static readonly ConcurrentDictionary<Tuple<Type, MethodInfo>, Lazy<LambdaExpression>> Cache =
            new ConcurrentDictionary<Tuple<Type, MethodInfo>, Lazy<LambdaExpression>>();

        static readonly ConcurrentStack<Tuple<Type, MethodInfo>> callStack =
            new ConcurrentStack<Tuple<Type, MethodInfo>>();

        static readonly Func<Tuple<Type, MethodInfo>, Lazy<LambdaExpression>> DecompileDelegate =
            t => new Lazy<LambdaExpression>(() => (LambdaExpression)new DecompileExpressionVisitor().Visit(MethodBodyDecompiler.Decompile(t.Item2, t.Item1)));
            //t => new Lazy<LambdaExpression>(() => MethodBodyDecompiler.Decompile(t.Item2, t.Item1));

        public static LambdaExpression Decompile(this Delegate @delegate)
        {
            var expression = Decompile(@delegate.Method);
            if (@delegate.Method.IsStatic) return expression;

            var visitor = new ReplaceExpressionVisitor(new Dictionary<Expression, Expression>
            {
                {expression.Parameters[0], Expression.Constant(@delegate.Target)}
            });
            var transformed = visitor.Visit(expression.Body);
            return Expression.Lambda(transformed, expression.Parameters.Skip(1));
        }

        public static LambdaExpression Decompile(this MethodInfo method)
        {
            return Decompile(method, method.DeclaringType);
        }

        public static LambdaExpression Decompile(this MethodInfo method, Type declaringType)
        {
            LambdaExpression result;
            var cacheKey = Tuple.Create(declaringType, method);
            if (callStack.Contains(cacheKey))
            {
                var message = "Possible infinite loop dectected : \n"
                    + method.ReflectedType +"."+ method.ToString().Split(' ')[1] + "\n\tcalled by "
                    + string.Join("\n\tcalled by ", callStack.Select(it => it.Item2.ReflectedType + "." + it.Item2.ToString().Split(' ')[1]))
                    ;

                IEnumerable<ParameterExpression> signature = new ParameterExpression[] { Expression.Parameter(declaringType, "this") }
                    .Union(method.GetParameters().Select(it => Expression.Parameter(it.ParameterType, it.Name)));
                var emptyCallParameters = method.IsStatic ? signature : signature.Skip(1);
                if (!Configuration.ThrowExceptionsOnDecompilationLoops) {
                    return Expression.Lambda(Expression.Block(Expression.DebugInfo(Expression.SymbolDocument("/* " + message + " */"), 1, 1, 1, 1), Expression.Default(method.ReturnType)), emptyCallParameters.ToArray());
                    //return Expression.Lambda(ExpressionHelper.Default(method.ReturnType, null), emptyCallParameters.ToArray());
                }
                throw new NotSupportedException(message);
            }
            callStack.Push(cacheKey);
            result = Cache.GetOrAdd(cacheKey, DecompileDelegate).Value;
            if (!callStack.TryPop(out cacheKey))
            {
                throw new Exception("Mishandled stack");
            }
            return result;
        }

        public static IQueryable<T> Decompile<T>(this IQueryable<T> self)
        {
            var provider = new DecompiledQueryProvider(self.Provider);
            return provider.CreateQuery<T>(self.Expression);
        }
    }
}
