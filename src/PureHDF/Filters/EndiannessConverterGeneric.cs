using System.Runtime.CompilerServices;

namespace PureHDF.Filters;

/// <summary>
/// A helper class to convert the endianness of data using hardware acceleration if available.
/// </summary>
public static class EndiannessConverterGeneric
{
    /// <summary>
    /// Converts the endianness of the given source buffer into the destination buffer.
    /// </summary>
    /// <param name="bytesOfType">The number of bytes of the underlying data type.</param>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    public unsafe static void Convert(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        // Actually, only the generic algorithm requires a dedicated destination buffer.
        // Problem: If new buffer is created in this method, hardware accelerated methods
        // won't know about that new buffer. If instead new buffer is created in unsafe
        // overload below, the newly created pointer would be lost. Returning new buffer
        // as simple return values would be possible, but requires a copy operation
        // from source to new destination in the hardware accelerated implementions
        // when they hand over control to the generic algorithm.
        // Concluding, it is easier to work with independent source and destination buffers.
        fixed (byte* src = source, dest = destination)
        {
            EndiannessConverterGeneric.Convert(bytesOfType, 0, source.Length, src, dest);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void Convert(int type_size, int vectorizable_blocksize, int blocksize, byte* source, byte* destination)
    {
        if (type_size == 1)
        {
            Buffer.MemoryCopy(
                source,
                destination,
                blocksize,
                blocksize);

            return;
        }

        int i, j;

        /* Calculate the number of elements in the block. */
        int neblock_quot = blocksize / type_size;
        int neblock_rem = blocksize % type_size;
        int vectorizable_elements = vectorizable_blocksize / type_size;

        /* Non-optimized shuffle */
        for (j = vectorizable_elements; j < neblock_quot; j++)
        {
            var offset = j * type_size;

            for (i = 0; i < type_size; i++)
            {
                destination[offset + i] = source[offset + type_size - i - 1];
            }
        }

        /* Copy any leftover bytes in the block without shuffling them. */
        Buffer.MemoryCopy(
            source + (blocksize - neblock_rem),
            destination + (blocksize - neblock_rem),
            neblock_rem,
            neblock_rem);
    }
}