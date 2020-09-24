using System;

namespace HDF5.NET
{
    public static class SpanExtensions
    {
        public static void Fill(this Span<byte> buffer, byte[] fillValue)
        {
            var size = fillValue.Length;

            for (int i = 0; i < buffer.Length; i++)
            {
                var fillValueIndex = i % size;
                buffer[i] = fillValue[fillValueIndex];
            }
        }
    }
}
