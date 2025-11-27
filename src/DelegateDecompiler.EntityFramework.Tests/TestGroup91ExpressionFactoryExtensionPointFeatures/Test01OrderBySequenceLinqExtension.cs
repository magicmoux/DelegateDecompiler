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
        [ExpressionFactory]
        private static Expression BuildOrderBySequenceExpression<T, TKey>(Expression source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)
            where T : class
            where TKey : IEquatable<TKey>
        {
            var sourceType = source.Type;
            if (!typeof(IQueryable<T>).IsAssignableFrom(sourceType) && !typeof(IEnumerable<T>).IsAssignableFrom(sourceType))
            {
                throw new ArgumentException("The source argument must be of type IQueryable<T> or IEnumerable<T>");
            }

            if (orderedKeys == null) orderedKeys = new List<TKey>();

            var values = orderedKeys as IList<TKey> ?? orderedKeys.ToList();

            Expression body = Expression.Constant(int.MaxValue, typeof(int));

            for (var i = values.Count - 1; i >= 0; i--)
            {
                var valueConstant = Expression.Constant(values[i], typeof(TKey));
                var match = Expression.Equal(keySelector.Body, valueConstant);
                var indexConstant = Expression.Constant(i, typeof(int));
                body = Expression.Condition(match, indexConstant, body);
            }

            var selectorParameter = keySelector.Parameters.First();
            var orderingLambda = Expression.Lambda(body, selectorParameter);

            var orderMethodProvider = typeof(IQueryable<T>).IsAssignableFrom(sourceType) ? typeof(Queryable) : typeof(Enumerable);
            return Expression.Call(orderMethodProvider, "OrderBy", new Type[] { typeof(T), typeof(int) }, source, orderingLambda);
        }

        [Decompile]
        public static IOrderedQueryable<T> OrderBySequence<T, TKey>(this IQueryable<T> source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)
            where T : class
            where TKey : IEquatable<TKey>
        {
            return (IOrderedQueryable<T>)source.Provider.CreateQuery(BuildOrderBySequenceExpression(source.Expression, keySelector, orderedKeys));
        }

        [Decompile]
        public static IOrderedEnumerable<T> OrderBySequence<T, TKey>(this IEnumerable<T> source, Expression<Func<T, TKey>> keySelector, IEnumerable<TKey> orderedKeys)
            where T : class
            where TKey : IEquatable<TKey>
        {
            return (IOrderedEnumerable<T>)BuildOrderBySequenceExpression(Expression.Constant(source), keySelector, orderedKeys);
        }

        [Computed]
        public static int GetFirstChildIdByParent(this EfTestDbContext context, int pId, IEnumerable<int> explicitOrder)
        {
            return context.EfChildren.Where(a => a.EfParentId == pId).OrderBySequence(c => c.EfChildId, explicitOrder).Select(b => b.EfChildId).FirstOrDefault();
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
                var explicitOrder = new int[] { 2, 3, 1 }.ToList();

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

                env.AboutToUseDelegateDecompiler();

                var query = env.Db.EfParents
                .Select(p => new
                {
                    EfParentId = p.EfParentId,
                    Children = p.Children
                        .OrderBySequence(c => c.EfChildId, explicitOrder)
                        .FirstOrDefault()
                })
#if NO_AUTO_DECOMPILE
                .Decompile()
#endif
                    ;
                var dd = query.ToList();

                env.CompareAndLogList(linq, dd);
            }
        }

    }
}