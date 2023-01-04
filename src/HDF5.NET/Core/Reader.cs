// if only .NET 7 is being targeted, the optimal solution to avoid two code paths
// for sync and async would be to use static abstract interface members:
// https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7/
// => search for "generic specialization".
//
// Why generic instead of "useAsync" parameter? Right now there is no difference
// but with the static abstract interface member approach of .NET 7, 
// the dummy parameter can be removed which means more efficiency.

namespace HDF5.NET
{
    interface IReader
    {
        ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer);
    }

    struct SyncReader : IReader
    {
        public ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer)
            => new ValueTask<int>(stream.Read(buffer.Span));
    }

    struct AsyncReader : IReader
    {
        public ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer) =>
            stream.ReadAsync(buffer);
    }
}