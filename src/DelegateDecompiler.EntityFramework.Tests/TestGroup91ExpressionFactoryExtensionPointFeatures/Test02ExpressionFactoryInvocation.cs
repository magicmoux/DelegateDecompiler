using NUnit.Framework;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DelegateDecompiler.EntityFramework.Tests.EfItems;
using DelegateDecompiler.EntityFramework.Tests.EfItems.Concretes;

namespace DelegateDecompiler.EntityFramework.Tests.TestGroup91ExpressionFactoryExtensionPointFeatures
{
    [TestFixture]
    public class Test02ExpressionFactoryInvocation
    {
        [Test]
        public void BuildOrderBySequenceExpression_IsExpanded_To_OrderBy_Call()
        {
            // Arrange: construct call to the private BuildOrderBySequenceExpression<EfChild,int>
            var sourceQueryable = Enumerable.Empty<EfChild>().AsQueryable();
            // pass the queryable's Expression wrapped as an Expression constant (factory expects Expression parameter)
            var sourceExpr = Expression.Constant(sourceQueryable.Expression, typeof(Expression));

            Expression<Func<EfChild, int>> keySelector = c => c.EfChildId;

            var orderedKeys = new[] { 2, 3, 1 }.ToList();
            var orderedKeysExpr = Expression.Constant(orderedKeys, typeof(System.Collections.Generic.List<int>));

            var method = typeof(OrderBySequenceLinqExtension).GetMethod("BuildOrderBySequenceExpression", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Could not find BuildOrderBySequenceExpression method");
            var generic = method.MakeGenericMethod(typeof(EfChild), typeof(int));

            var call = Expression.Call(generic, sourceExpr, keySelector, orderedKeysExpr);

            // Act: run factory expansion using public JIT visitor
            var expanded = DelegateDecompiler.JIT.ExpressionFactoryVisitor.Build(call);

            // Assert: result should be a call to Queryable.OrderBy or Enumerable.OrderBy
            Assert.IsInstanceOf<MethodCallExpression>(expanded);
            var mcall = (MethodCallExpression)expanded;
            Assert.That(mcall.Method.Name, Is.EqualTo("OrderBy"), "Factory should produce an OrderBy call");
            Assert.That(mcall.Method.DeclaringType == typeof(Queryable) || mcall.Method.DeclaringType == typeof(Enumerable));
        }
    }
}
