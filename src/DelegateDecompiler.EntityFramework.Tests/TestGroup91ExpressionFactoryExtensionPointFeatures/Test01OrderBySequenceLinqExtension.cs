using DelegateDecompiler.EntityFramework.Tests.EfItems;
using DelegateDecompiler.EntityFramework.Tests.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
#if EF_CORE
using Microsoft.EntityFrameworkCore;
#endif

namespace DelegateDecompiler.EntityFramework.Tests.TestGroup91ExpressionFactoryExtensionPointFeatures
{
    public static class OrderBySequenceLinqExtension
    {
        /// <summary>
        /// This extension method allows ordering an IQueryable based on an explicit sequence of keys.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="explicitSequence"></param>
        /// <param name="matchKeySelector"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [ExpressionFactory]
        private static Expression BuildOrderBySequenceExpression<T, TKey>(Expression source, IEnumerable<TKey> explicitSequence, Expression<Func<T, TKey>> matchKeySelector)
            where T : class
            where TKey : IEquatable<TKey>
        {
            Expression expr = source is LambdaExpression func ? func.Body : source;
            var targetType = expr.Type;
            if (!typeof(IQueryable<T>).IsAssignableFrom(targetType) && !typeof(IEnumerable<T>).IsAssignableFrom(targetType))
            {
                throw new ArgumentException("The source parameter must be of type IQueryable<T> or IEnumerable<T>");
            }

            Expression body = Expression.Constant(int.MaxValue);
            LambdaExpression lambda = matchKeySelector;
            var selectorParameter = matchKeySelector.Parameters.First();
            var valuesList = explicitSequence?.ToList() ?? new List<TKey>();
            if (valuesList.Any())
            {
                for (var i = 0; i < valuesList.Count; i++)
                {
                    body = Expression.Condition(Expression.Equal(Expression.Constant(valuesList[i]), matchKeySelector.Body), Expression.Constant(i), body);
                }
                lambda = (Expression<Func<T, int>>)Expression.Lambda(body, selectorParameter);
            }
            if (typeof(IQueryable<T>).IsAssignableFrom(targetType))
            {
                return Expression.Call(typeof(Queryable), "OrderBy", new Type[] { typeof(T), typeof(int) }, expr, lambda);
            }
            else
            {
                return Expression.Call(typeof(Enumerable), "OrderBy", new Type[] { typeof(T), typeof(int) }, expr, lambda);
            }
        }

        [Decompile]
        public static IOrderedQueryable<T> OrderBySequence<T, TKey>(this IQueryable<T> source, IEnumerable<TKey> explicitSequence, Expression<Func<T, TKey>> matchSelector)
            where T : class
            where TKey : IEquatable<TKey>
        {
            return (IOrderedQueryable<T>)source.Provider.CreateQuery(BuildOrderBySequenceExpression(source.Expression, explicitSequence, matchSelector));
        }

        [Decompile]
        public static IList<T> OrderBySequence<T, TKey>(this IEnumerable<T> source, IEnumerable<TKey> explicitSequence, Expression<Func<T, TKey>> matchSelector)
            where T : class
            where TKey : IEquatable<TKey>
        {
            return (IList<T>)BuildOrderBySequenceExpression(() => source, explicitSequence, matchSelector);
        }

        [Computed]
        public static int GetFirstChildIdByParent(this EfTestDbContext context, int pId, IEnumerable<int> explicitOrder)
        {
            return context.EfChildren.Where(a => a.EfParentId == pId).OrderBySequence(explicitOrder, c => c.EfChildId).Select(b => b.EfChildId).FirstOrDefault();
        }
    }


    class Test01OrderBySequenceLinqExtension
    {
        private ClassEnvironment classEnv;

        [OneTimeSetUp]
        public void SetUpFixture()
        {
            classEnv = new ClassEnvironment();
        }

        class ParentIdWithFirstChildId
        {
            public int ParentId { get; set; }
            public int FirstChildId { get; set; }

            public override bool Equals(object obj)
            {
                return obj is ParentIdWithFirstChildId id && id.ParentId == ParentId && id.FirstChildId == FirstChildId;
            }

            public override int GetHashCode()
            {
                return ParentId * 131 + FirstChildId;
            }
        }

        [Test]
        public void TestBasicOrderBySequenceExtension()
        {
            using (var env = new MethodEnvironment(classEnv))
            {
                var explicitOrder = new int[] { 2, 3, 1 };

                //SETUP
                var linq = env.Db.EfParents
                    .Select(p => new
                    {
                        EfParentId = p.EfParentId,
                        Children = p.Children
                            .OrderBy(c =>
                                c.EfChildId == 2 ? 1 :
                                c.EfChildId == 3 ? 2 :
                                c.EfChildId == 1 ? 3 :
                                (int?)null)
                            .FirstOrDefault()
                    })
                    .ToList();

                //ATTEMPT
                env.AboutToUseDelegateDecompiler();

                var query = env.Db.EfParents
                .Select(p => new
                {
                    EfParentId = p.EfParentId,
                    Children = p.Children
                        .OrderBySequence(explicitOrder, c => c.EfChildId)
                        .FirstOrDefault()
                })
#if NO_AUTO_DECOMPILE
                .Decompile()
#endif
                    ;
                var dd = query.ToList();

                //VERIFY
                env.CompareAndLogList(linq, dd);
            }
        }

    }
}