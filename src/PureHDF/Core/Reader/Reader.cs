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
        ValueTask<int> ReadDatasetAsync(Stream stream, Memory<byte> buffer, long offset);
    }

    struct SyncReader : IReader
    {
        public ValueTask<int> ReadDatasetAsync(Stream stream, Memory<byte> buffer, long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            // #error Actual stream is wrapped
            return new ValueTask<int>(stream switch 
            {
                IDatasetStream datasetStream => datasetStream.ReadDataset(buffer),
                _ => stream.Read(buffer.Span)
            });
        }
    }

    struct AsyncReader : IReader
    {
        public ValueTask<int> ReadDatasetAsync(Stream stream, Memory<byte> buffer, long offset)
        {
            // #error Actual stream is wrapped
            stream.Seek(offset, SeekOrigin.Begin);
            
            return stream switch 
            {
                IDatasetStream datasetStream => datasetStream.ReadDatasetAsync(buffer),
                _ => stream.ReadAsync(buffer)
            };
        }
    }
}