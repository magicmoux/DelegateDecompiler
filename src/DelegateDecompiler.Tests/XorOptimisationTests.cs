using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;

namespace DelegateDecompiler.Tests
{
    [TestFixture]
    public class XorOptimisationTests : DecompilerTestsBase
    {

        [Test(Description = "Trivial case of A ^ !A")]
        public void Test_Parameter_Xor_NotParameter_RewritesToTrue()
        {
            // This is a trivial case of A ^ !A 
            Expression<Func<bool, bool>> expected = value => true;
            Func<bool, bool> compiled = value => value ^ !value;

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.Constant));
            Assert.That(((ConstantExpression)decompiled.Body).Value, Is.EqualTo(true));
        }

        [Test(Description = "Inferred case of A ^ !A through A ^ B having A <=> !B"), Ignore("Not yet capable of optimizing A ^ B where A = !B")]
        public void Test_Parameter_Xor_AllCatching_RewritesToTrue()
        {
            Expression<Func<int, bool>> expected = value => true;
            Func<int, bool> compiled = value => value < 5 ^ value >= 5;

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.Constant));
            Assert.That(((ConstantExpression)decompiled.Body).Value, Is.EqualTo(true));
        }

        [Test(Description = "Case of !Any() ^ Contains(value)")]
        public void Test_NotAny_Xor_Contains_RewritesToOrElse()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => !items.Any() || items.Contains(value);
            Func<IList<int>, int, bool> compiled = (items, value) => !items.Any() ^ items.Contains(value);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.OrElse));
        }

        [Test(Description = "Inferring previous case using !Any() ^ Any(x => x == value)")]
        public void Test_NotAny_Xor_AnyEquals_RewritesToOrElse()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => !items.Any() || items.Any(x => x == value);
            Func<IList<int>, int, bool> compiled = (items, value) => !items.Any() ^ items.Any(x => x == value);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.OrElse));
        }

        [Test(Description = "Inferring previous case using !Any() ^ Where(predicate).Any()"), Ignore("Not yet capable of inferring Any(predicate) <=> Where(predicate).Any()")]
        public void Test_NotAny_Xor_WhereAny_RewritesToOrElse()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => !items.Any() || items.Where(x => x == value).Any();
            Func<IList<int>, int, bool> compiled = (items, value) => !items.Any() ^ items.Where(x => x == value).Any();

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.OrElse));
        }

        [Test]
        public void Test_Any_Xor_NotContains_RewritesToOrElse()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => items.Any() || !items.Contains(value);
            Func<IList<int>, int, bool> compiled = (items, value) => items.Any() ^ !items.Contains(value);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.OrElse));
        }

        [Test]
        public void Test_OrderInvariant_Contains_Xor_NotAny_RewritesToOrElse()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => items.Contains(value) || !items.Any();
            Func<IList<int>, int, bool> compiled = (items, value) => items.Contains(value) ^ !items.Any();

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.OrElse));
        }

        [Test]
        public void Test_Any_Xor_Contains_NoRewrite_NotMutuallyExclusive()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => items.Any() ^ items.Contains(value);
            Func<IList<int>, int, bool> compiled = (items, value) => items.Any() ^ items.Contains(value);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.ExclusiveOr));
        }

        [Test]
        public void Test_DifferentSources_NotRewritten()
        {
            Expression<Func<IList<int>, IList<int>, int, bool>> expected = (left, right, value) => !left.Any() ^ right.Contains(value);
            Func<IList<int>, IList<int>, int, bool> compiled = (left, right, value) => !left.Any() ^ right.Contains(value);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.ExclusiveOr));
        }

        [Test]
        public void Test_UnrelatedXor_NotRewritten()
        {
            Expression<Func<IList<int>, int, bool>> expected = (items, value) => items.Contains(value) ^ (value > 0);
            Func<IList<int>, int, bool> compiled = (items, value) => items.Contains(value) ^ (value > 0);

            var decompiled = Test(compiled, expected);

            Assert.That(decompiled.Body.NodeType, Is.EqualTo(ExpressionType.ExclusiveOr));
        }
    }
}
