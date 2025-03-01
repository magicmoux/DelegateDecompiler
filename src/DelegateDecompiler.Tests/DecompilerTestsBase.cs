using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace DelegateDecompiler.Tests
{
    public static class DecompilerTestsBaseExtensions
    {
        static Func<LambdaExpression, string> matchesBody = expected => expected.Body.ToString();
        static Func<LambdaExpression, string> matchesDebugView = expected => DecompilerTestsBase.DebugView(expected.Body);

        public static void DecompilationShouldMatch<T>(this T compiled, params Expression<T>[] expected)
            where T : Delegate
        {
            DecompilationShouldMatch(compiled, true, expected);
        }


        public static void DecompilationShouldMatch<T>(this T compiled, bool compareDebugView, params Expression<T>[] expected)
        {
            //Double cast required as we can not convert T to Delegate directly
            var decompiled = ((Delegate)(object)compiled).Decompile();

            var expectation = Is.EqualTo(matchesBody(expected.First()));
            foreach (var expression in expected.Skip(1))
            {
                expectation = expectation.Or.EqualTo(matchesBody(expression));
            }
            Assert.That(decompiled.Body.ToString(), expectation);
            if (compareDebugView)
            {
                expectation = Is.EqualTo(matchesDebugView(expected.First()));
                foreach (var expression in expected.Skip(1))
                {
                    expectation = expectation.Or.EqualTo(matchesDebugView(expression));
                }
                Assert.That(DecompilerTestsBase.DebugView(decompiled.Body), expectation);
            }

        }

    }

    public class DecompilerTestsBase
    {
        internal static readonly Func<Expression, string> DebugView = BuildDebugView();

        private static Func<Expression, string> BuildDebugView()
        {
            var parameter = Expression.Parameter(typeof(Expression), "e");
            return Expression.Lambda<Func<Expression, string>>(Expression.Property(parameter, "DebugView"), parameter).Compile();
        }

        protected static void Test<T>(Expression<T> expected, T compiled)
        {
            //Double cast required as we can not convert T to Delegate directly
            var decompiled = ((Delegate)((object)compiled)).Decompile();

            var x = expected.Body.ToString();
            Console.WriteLine(x);
            var y = decompiled.Body.ToString();
            Console.WriteLine(y);
            Assert.That(y, Is.EqualTo(x));
            Assert.That(DebugView(decompiled.Body), Is.EqualTo(DebugView(expected.Body)));
        }

        protected static void Test<T>(Expression<T> expected, MethodInfo compiled)
        {
            //Double cast required as we can not convert T to Delegate directly
            var decompiled = compiled.Decompile();

            var x = expected.Body.ToString();
            Console.WriteLine(x);
            var y = decompiled.Body.ToString();
            Console.WriteLine(y);
            Assert.That(y, Is.EqualTo(x));
            Assert.That(DebugView(decompiled.Body), Is.EqualTo(DebugView(expected.Body)));
        }

        protected static void Test<T>(Expression<T> expected1, Expression<T> expected2, T compiled, bool compareDebugView = true)
        {
            //Double cast required as we can not convert T to Delegate directly
            var decompiled = ((Delegate)((object)compiled)).Decompile();

            var x1 = expected1.Body.ToString();
            Console.WriteLine(x1);
            var x2 = expected2.Body.ToString();
            Console.WriteLine(x2);
            var y = decompiled.Body.ToString();
            Console.WriteLine(y);
            Assert.That(y, Is.EqualTo(x1).Or.EqualTo(x2));
            if (compareDebugView)
                Assert.That(DebugView(decompiled.Body), Is.EqualTo(DebugView(expected1.Body)).Or.EqualTo(DebugView(expected2.Body)));
        }

        protected static void AssertAreEqual(Expression expected, Expression actual, bool compareDebugView = true)
        {
            Assert.That(actual.ToString(), Is.EqualTo(expected.ToString()));
            if (compareDebugView)
                Assert.That(DebugView(actual), Is.EqualTo(DebugView(expected)));
        }

        protected static void AssertAreEqual(Expression expected1, Expression expected2, Expression actual, bool compareDebugView = true)
        {
            Assert.That(actual.ToString(), Is.AnyOf(expected1.ToString(), expected2.ToString()));
            if (compareDebugView)
                Assert.That(DebugView(actual), Is.AnyOf(DebugView(expected1), DebugView(expected2)));
        }
    }
}
