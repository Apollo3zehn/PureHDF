using System;

namespace HDF5.NET
{
    internal static class SpanExtensions
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

        public static Memory<TTo> Cast<TFrom, TTo>(this Memory<TFrom> from)
            where TFrom : struct
            where TTo : struct
        {
            // avoid the extra allocation/indirection, at the cost of a gen-0 box
            if (typeof(TFrom) == typeof(TTo))
                return (Memory<TTo>)(object)from;

            return new CastMemoryManager<TFrom, TTo>(from).Memory;
        }
    }
}
