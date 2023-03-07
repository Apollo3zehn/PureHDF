// If only .NET 7 is being targeted, the optimal solution to avoid two code paths
// for sync and async would be to use static abstract interface members:
// https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7/
// => search for "generic specialization".
//
// Why generic instead of "useAsync" parameter? Right now there is no difference
// but with the static abstract interface member approach of .NET 7, 
// the dummy parameter can be removed which means more efficiency.

namespace PureHDF
{
    interface IReader
    {
        ValueTask ReadDatasetAsync(IH5ReadStream stream, Memory<byte> buffer, long offset);
    }

    struct SyncReader : IReader
    {
        public ValueTask ReadDatasetAsync(IH5ReadStream stream, Memory<byte> buffer, long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Read(buffer);

#if NET5_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask();
#endif
        }
    }

    struct AsyncReader : IReader
    {
        public ValueTask ReadDatasetAsync(IH5ReadStream stream, Memory<byte> buffer, long offset)
        {
#if NET6_0_OR_GREATER
            stream.Seek(offset, SeekOrigin.Begin);
            return stream.ReadAsync(buffer);
#else
            throw new Exception("Async read operations are not supported on this runtime.");
#endif
        }
    }
}