using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DelegateDecompiler.JIT
{
    internal static class ExpressionFactoryInvoker
    {
        public static bool TryInvokeFactory(MethodInfo method, object instanceObj, object[] args, out Expression expression)
        {
            expression = null;
            if (!method.IsDefined(typeof(DelegateDecompiler.ExpressionFactoryAttribute), true)) return false;
            try
            {
                var result = method.Invoke(instanceObj, args);
                expression = result as Expression;
                return expression != null;
            }
            catch
            {
                return false;
            }
        }

        public static object TryCreateInstance(Type targetType, object value)
        {
            if (value == null) return null;
            var ctors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                var ps = ctor.GetParameters();
                if (ps.Length != 1) continue;
                var pType = ps[0].ParameterType;
                if (pType.IsInstanceOfType(value))
                {
                    try { return ctor.Invoke(new object[] { value }); } catch { }
                }
                if (pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    if (value is IEnumerable)
                    {
                        try { return ctor.Invoke(new object[] { value }); } catch { }
                    }
                }
            }
            if (targetType.IsArray && value is IEnumerable enumerable)
            {
                var elemType = targetType.GetElementType();
                var toArrayMethod = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "ToArray" && m.GetParameters().Length == 1);
                if (toArrayMethod != null)
                {
                    var gen = toArrayMethod.MakeGenericMethod(elemType);
                    try { return gen.Invoke(null, new object[] { enumerable }); } catch { }
                }
            }
            return null;
        }
    }
}
