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

    public static void Write(this Stream stream, Span<byte> buffer)
    {
        stream.Write(buffer.ToArray(), 0, buffer.Length);
    }
}

#endif