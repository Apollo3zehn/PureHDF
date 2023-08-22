#if NETSTANDARD2_0

using System.Buffers;
using System.Runtime.InteropServices;

namespace PureHDF;

// source adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/StreamExtensions.netstandard.cs
internal static partial class StreamExtensions
{
    public static int Read(this Stream stream, Span<byte> buffer)
    {
        var length = (int)Math.Min(buffer.Length, stream.Length - stream.Position);

        var tmpBuffer = new byte[length];
        stream.Read(tmpBuffer, 0, length);
        tmpBuffer.CopyTo(buffer);

        return length;
    }

    public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
        {
            return new ValueTask<int>(stream.ReadAsync(array.Array, array.Offset, array.Count, cancellationToken));
        }
        else
        {
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            return FinishReadAsync(stream.ReadAsync(sharedBuffer, 0, buffer.Length, cancellationToken), sharedBuffer, buffer);

            static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
            {
                try
                {
                    int result = await readTask.ConfigureAwait(false);
                    new Span<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
                    return result;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(localBuffer);
                }
            }
        }
    }

    public static void Write(this Stream stream, Span<byte> buffer)
    {
        stream.Write(buffer.ToArray());
    }
}

#endif