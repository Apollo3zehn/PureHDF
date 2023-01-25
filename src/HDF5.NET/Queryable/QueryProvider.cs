using System.Linq.Expressions;
using System.Reflection;

namespace HDF5.NET
{
    internal class QueryProvider<TFake> : IQueryProvider where TFake : unmanaged
    {
        private readonly ulong _datasetLength;
        private readonly Func<HyperslabSelection, TFake[]> _executor;

        private ulong _start;
        private ulong _stride;
        private ulong _count;
        private ulong _block;

        public QueryProvider(ulong datasetLength, Func<HyperslabSelection, TFake[]> executor)
        {
            _datasetLength = datasetLength;
            _executor = executor;
        }

        public IQueryable CreateQuery(Expression expression) => throw new NotImplementedException();

        public IQueryable<T> CreateQuery<T>(Expression expression)
        {
            return new Queryable<T>(this, expression);
        }

        public object? Execute(Expression expression) => throw new NotImplementedException();

        public T Execute<T>(Expression expression)
        {
            ProcessExpression(expression);

            if (_stride == 0 && _block == 0)
            {
                _stride = _datasetLength - _start;
                _block = _datasetLength - _start;
            }

            if (_stride == 0 && _block != 0)
                _stride = _block;

            if (_count == 0)
                _count = 1;

            var fileSelection = new HyperslabSelection(_start, _stride, _count, _block);

            return (T)(object)_executor(fileSelection);
        }

        private void ProcessMethodCallExpression(MethodCallExpression methodCall)
        {
            var parameter = (ulong)GetIntFromExpression(methodCall.Arguments[1]);

            switch (methodCall.Method.Name)
            {
                case "Skip":
                    _start = parameter;
                    ProcessExpression(methodCall.Arguments[0]);
                    break;

                case "Stride":
                    _stride = parameter;
                    ProcessExpression(methodCall.Arguments[0]);
                    break;

                case "Repeat":
                    _count = parameter;
                    ProcessExpression(methodCall.Arguments[0]);
                    break;

                case "Take":
                    _block = parameter;
                    ProcessExpression(methodCall.Arguments[0]);
                    break;

                default:
                    throw new Exception($"Unsupported method {methodCall.Method.Name}.");
            };
        }

        private void ProcessExpression(Expression expression)
        {
            switch (expression)
            {
                case MethodCallExpression methodCall:
                    ProcessMethodCallExpression(methodCall);
                    break;

                case ConstantExpression:
                    break;

                default:
                    throw new Exception($"Unsupported expression {expression.GetType().FullName}.");
            }
        }

        private static int GetIntFromExpression(Expression expression)
        {
            if (expression is ConstantExpression constant && constant.Value is int value)
                return value;

            throw new Exception($"Unable to extract integer from expression.");
        }

        // private static string? GetDebugView(Expression expression)
        // {
        //     if (expression is null)
        //         return null;

        //     var propertyInfo = typeof(Expression)
        //         .GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic)!;

        //     return propertyInfo.GetValue(expression) as string;
        // }
    }
}