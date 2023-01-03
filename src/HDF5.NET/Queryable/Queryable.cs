using System.Collections;
using System.Linq.Expressions;

namespace HDF5.NET
{
    internal class Queryable<T> : IQueryable<T>
    {
        internal Queryable(IQueryProvider provider, Expression? expression = default)
        {
            Provider = provider;
            Expression = expression ?? Expression.Constant(this);
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
        {
            return Provider
                .Execute<IEnumerable<T>>(Expression)
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }
}