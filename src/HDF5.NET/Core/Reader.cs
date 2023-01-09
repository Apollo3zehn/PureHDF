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
        ValueTask<int> ReadAsync(H5BinaryReader reader, Memory<byte> buffer, long offset);
        ValueTask<int> ReadAsync(H5Stream stream, Memory<byte> buffer, long offset);
    }

    struct SyncReader : IReader
    {
        // See notes/async.md for a thread-safety analysis.
        public ValueTask<int> ReadAsync(H5BinaryReader reader, Memory<byte> buffer, long offset)
        {
            if (reader.SafeFileHandle is null)
            {
                reader.Seek(offset, SeekOrigin.Begin);
                return new ValueTask<int>(reader.BaseStream.Read(buffer.Span));
            }
            else
            {
#if NET6_0_OR_GREATER
                return new ValueTask<int>(RandomAccess.Read(reader.SafeFileHandle, buffer.Span, offset));
#else
                reader.Seek(offset, SeekOrigin.Begin);
                return new ValueTask<int>(reader.BaseStream.Read(buffer.Span));
#endif
            }
        }

        // See notes/async.md for a thread-safety analysis.
        public ValueTask<int> ReadAsync(H5Stream stream, Memory<byte> buffer, long offset)
        {
            if (stream.SafeFileHandle is null)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                return new ValueTask<int>(stream.Read(buffer.Span));
            }
            else
            {
#if NET6_0_OR_GREATER
                return new ValueTask<int>(RandomAccess.Read(stream.SafeFileHandle, buffer.Span, stream.SafeFileHandleOffset + offset));
#else
                stream.Seek(offset, SeekOrigin.Begin);
                return new ValueTask<int>(stream.Read(buffer.Span));
#endif
            }
        }
    }

    struct AsyncReader : IReader
    {
        // See notes/async.md for a thread-safety analysis.
        public ValueTask<int> ReadAsync(H5BinaryReader reader, Memory<byte> buffer, long offset)
        {
            if (reader.SafeFileHandle is null)
            {
                throw new Exception("Asynchronous read operations are only supported for file streams.");
            }

            else
            {
#if NET6_0_OR_GREATER
                return RandomAccess.ReadAsync(reader.SafeFileHandle, buffer, offset);
#else
                throw new Exception("Asynchronous read operations are only supported on .NET 6+.");
#endif
            }
            
        }

        // See notes/async.md for a thread-safety analysis.
        public ValueTask<int> ReadAsync(H5Stream stream, Memory<byte> buffer, long offset)
        {
            if (stream.SafeFileHandle is null)
            {
                if (stream.IsStackOnly)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    return stream.ReadAsync(buffer);
                }
                else
                {
                    throw new Exception($"The stream of type {stream.GetType().FullName} is not thread-safe.");
                }
            }

            else
            {
#if NET6_0_OR_GREATER
                return RandomAccess.ReadAsync(stream.SafeFileHandle, buffer, stream.SafeFileHandleOffset + offset);
#else
                throw new Exception("Asynchronous read operations are only supported on .NET 6+.");
#endif
            }
            
        }
    }
}