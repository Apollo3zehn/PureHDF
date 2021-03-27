#if NETSTANDARD2_0

using System;
using System.IO;

namespace HDF5.NET
{
    internal static class StreamExtensions
    {
        public static int Read(this Stream stream, Span<byte> buffer)
        {
            var length = (int)Math.Min(buffer.Length, stream.Length - stream.Position);

            var tmpBuffer = new byte[length];
            stream.Read(tmpBuffer, 0, length);
            tmpBuffer.CopyTo(buffer);

            return length;
        }
    }
}

#endif