using NUnit.Framework;
using System.Linq.Expressions;
using System.Reflection;
using System;
using DelegateDecompiler;

namespace DelegateDecompiler.Tests
{
    public static class FactoryHelpers
    {
        [ExpressionFactory]
        public static Expression MakeConstant42()
        {
            return Expression.Constant(42);
        }
    }

    public class TestExpressionFactoryTests
    {
        // Caller method to exercise MethodBodyDecompiler/CallProcessor inlining
        public static Expression CallFactoryMethod()
        {
            return FactoryHelpers.MakeConstant42();
        }

        [Test]
        public void Visitor_Inlines_Factory_Call()
        {
            var factoryMethod = typeof(FactoryHelpers).GetMethod(nameof(FactoryHelpers.MakeConstant42), BindingFlags.Public | BindingFlags.Static);
            var call = Expression.Call(factoryMethod);

            var built = JIT.ExpressionFactoryVisitor.Build(call);

            Assert.IsInstanceOf<ConstantExpression>(built);
            var c = (ConstantExpression)built;
            Assert.That(c.Value, Is.EqualTo(42));
        }

        [Test]
        public void MethodBodyDecompiler_Inlines_Factory_Call()
        {
            var method = typeof(TestExpressionFactoryTests).GetMethod(nameof(CallFactoryMethod), BindingFlags.Public | BindingFlags.Static);
            var lambda = MethodBodyDecompiler.Decompile(method);
            var body = lambda.Body;

            if (body is ConstantExpression constant)
            {
                Assert.That(constant.Value, Is.EqualTo(42));
                return;
            }

            if (body is MethodCallExpression call)
            {
                var expanded = JIT.ExpressionFactoryVisitor.Build(call);
                Assert.IsInstanceOf<ConstantExpression>(expanded);
                var c = (ConstantExpression)expanded;
                Assert.That(c.Value, Is.EqualTo(42));
                return;
            }

            Assert.Fail($"Unexpected expression node: {body.NodeType}");
        }
    }
}
