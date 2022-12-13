#if NETSTANDARD2_0


namespace HDF5.NET
{
    internal static class BinaryReaderExtensions
    {
        public static void Read(this BinaryReader reader, Span<byte> buffer)
        {
            var tmpBuffer = new byte[buffer.Length];
            reader.Read(tmpBuffer, 0, tmpBuffer.Length);
            tmpBuffer.CopyTo(buffer);
        }
    }
}

#endif