using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace DelegateDecompiler
{
    internal static class XorOptimisationRules
    {
        internal static readonly Func<Expression, Expression, bool>[] MutualExclusionRules =
        {
            IsXorWithAnyExclusive,
        };

        //TODO: implement a real expression comparer that can handle more complex cases, for now just check for reference equality and simple ToString() equality.
        static bool ExpressionsAreEqual(Expression left, Expression right)
        {
            //TODO: Extend cases in phase 2, for now just check for reference equality and simple ToString() equality.
            return ReferenceEquals(left, right) || left.ToString() == right.ToString();
        }

        private static bool ExpressionsAreExclusive(LambdaExpression leftPredicate, LambdaExpression rightPredicate)
        {

            if ((leftPredicate == null && rightPredicate != null)
                || (leftPredicate != null && rightPredicate == null))
            {
                return true;
            }

            // TODO: check for simple cases where the predicates are the same except for a Not, e.g. x => x > 5 and x => !(x > 5)
            throw new NotImplementedException(""); //leftPredicate.ToString() == rightPredicate.ToString().Replace("!", "");
        }

        static bool TryGetLinqSource(MethodCallExpression call, out Expression source)
        {
            source = call.Object ?? (call.Arguments.Count >= 1 ? call.Arguments[0] : null);
            return source != null;
        }

        static bool IsAny(MethodCallExpression call, out Expression subject, out LambdaExpression predicate)
        {
            predicate = null;
            subject = null;
            if (call.Method.DeclaringType.Namespace == "System.Linq" && call.Method.Name == "Any")
            {
                subject = call.Arguments[0];
                if (call.Arguments.Count == 2)
                {
                    predicate = (LambdaExpression)call.Arguments[1];
                }
                return true;
            }
            return false;
        }

        static bool IsXorWithAnyExclusive(Expression rawLeft, Expression rawRight)
        {
            var left = OptimizeExpressionVisitor.StripNot(rawLeft, out var leftNegated);
            var right = OptimizeExpressionVisitor.StripNot(rawRight, out var rightNegated);
            if (leftNegated == rightNegated)
                return false;

            if (!(left is MethodCallExpression leftCall) || !(right is MethodCallExpression rightCall))
                return false;

            MethodCallExpression anyCall;
            MethodCallExpression otherCall;
            Expression anySource;
            Expression otherSource;

            if (IsAny(leftCall, out var leftSource, out var leftPredicate) && IsAny(rightCall, out var rightSource, out var rightPredicate))
            {
                return ExpressionsAreEqual(leftSource, rightSource)
                    && ExpressionsAreExclusive(leftPredicate, rightPredicate);
            }
            else if (IsAny(leftCall, out anySource, out leftPredicate) && TryGetLinqSource(rightCall, out otherSource))
            {
                anyCall = leftCall;
                otherCall = rightCall;
            }
            else if (IsAny(rightCall, out anySource, out rightPredicate) && TryGetLinqSource(leftCall, out otherSource))
            {
                anyCall = rightCall;
                otherCall = leftCall;
            }
            else
            {
                return false;
            }

            //TODO: Implement cases that depend on the other method specificity, e.g. Contains, All, etc. For now just check for the same source.
            return ExpressionsAreEqual(anySource, otherSource);
        }
    }
}
