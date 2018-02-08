using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ExpressionSerialization
{
    public static class RecursiveSelectExtensions
    {

        static IQueryable<TResult> recursiveSelect<TSource, TResult>(this IQueryable<TSource> source,
            Func<TSource, IEnumerable<TSource>> childSelectorFunc, Func<TSource, TResult> resultSelectorFunc)
        {
            return source
                .SelectMany(element =>
                                    recursiveSelect(childSelectorFunc(element).AsQueryable() ?? Enumerable.Empty<TSource>().AsQueryable(),
                                                    childSelectorFunc,resultSelectorFunc).Concat(Enumerable.Repeat(resultSelectorFunc(element), 1))
                );
        }

        public static IQueryable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
            Expression<Func<TSource, IEnumerable<TSource>>> childSelectorPredicate, Expression<Func<TSource, TResult>> resultSelectorPredicate)
        {
            return source.AsQueryable().recursiveSelect(childSelectorPredicate.Compile(), resultSelectorPredicate.Compile());
        }

        public static IQueryable<TSource> RecursiveSelect<TSource>(this IEnumerable<TSource> source,
                                                            Expression<Func<TSource, IEnumerable<TSource>>> childSelectorPredicate)
        {
            return source.AsQueryable().RecursiveSelect(childSelectorPredicate, element => element);
        }

        public static IQueryable<TResult> Evaluate<TResult>(this IQueryable<TResult> query)
        {
            var expr = Evaluator.PartialEval(query.Expression);
            return (IQueryable<TResult>)query.Provider.CreateQuery(expr);
        }
    }
}
