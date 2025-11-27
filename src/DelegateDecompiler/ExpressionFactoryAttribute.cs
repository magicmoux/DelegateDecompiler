using System;

namespace DelegateDecompiler
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ExpressionFactoryAttribute : Attribute
    {
    }
}
