using System.Linq.Expressions;
using System.Reflection;

namespace System.Linq
{
    internal static class H5QueryableExtensions
    {
        public static IQueryable<TSource> Repeat<TSource>(
            this IQueryable<TSource> source,
            int count)
        {
            var methodInfo = typeof(H5QueryableExtensions)
                .GetMethod(nameof(Repeat), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(new Type[] { typeof(TSource) });

            var expression = Expression.Call(
                instance: default,
                method: methodInfo,
                arg0: source.Expression,
                arg1: Expression.Constant(count));

            var query = source.Provider.CreateQuery<TSource>(expression);

            return query;
        }

        public static IQueryable<TSource> Stride<TSource>(
            this IQueryable<TSource> source,
            int count)
        {
            var methodInfo = typeof(H5QueryableExtensions)
                .GetMethod(nameof(Stride), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(new Type[] { typeof(TSource) });

            var expression = Expression.Call(
                instance: default,
                method: methodInfo,
                arg0: source.Expression,
                arg1: Expression.Constant(count));

            var query = source.Provider.CreateQuery<TSource>(expression);

            return query;
        }
    }
}