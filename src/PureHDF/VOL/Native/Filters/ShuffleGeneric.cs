using System.Runtime.CompilerServices;

namespace PureHDF.Filters
{
    internal static class ShuffleGeneric
    {
        public static unsafe void Shuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
        {
            fixed (byte* src = source, dest = destination)
            {
                ShuffleGeneric.shuffle_avx2(bytesOfType, 0, source.Length, src, dest);
            }
        }

        public static unsafe void Unshuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
        {
            fixed (byte* src = source, dest = destination)
            {
                ShuffleGeneric.unshuffle_avx2(bytesOfType, 0, source.Length, src, dest);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void shuffle_avx2(int type_size, int vectorizable_blocksize, int blocksize, byte* source, byte* destination)
        {
            int i, j;

            /* Calculate the number of elements in the block. */
            int neblock_quot = blocksize / type_size;
            int neblock_rem = blocksize % type_size;
            int vectorizable_elements = vectorizable_blocksize / type_size;

            /* Non-optimized shuffle */
            for (j = 0; j < type_size; j++)
            {
                for (i = vectorizable_elements; i < (int)neblock_quot; i++)
                {
                    destination[j * neblock_quot + i] = source[i * type_size + j];
                }
            }

            /* Copy any leftover bytes in the block without shuffling them. */
            Buffer.MemoryCopy(
                source + (blocksize - neblock_rem),
                destination + (blocksize - neblock_rem),
                neblock_rem,
                neblock_rem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void unshuffle_avx2(int type_size, int vectorizable_blocksize, int blocksize, byte* source, byte* destination)
        {
            int i, j;

            /* Calculate the number of elements in the block. */
            int neblock_quot = blocksize / type_size;
            int neblock_rem = blocksize % type_size;
            int vectorizable_elements = vectorizable_blocksize / type_size;

            /* Non-optimized unshuffle */
            for (i = vectorizable_elements; i < (int)neblock_quot; i++)
            {
                for (j = 0; j < type_size; j++)
                {
                    destination[i * type_size + j] = source[j * neblock_quot + i];
                }
            }

            /* Copy any leftover bytes in the block without unshuffling them. */
            Buffer.MemoryCopy(
                source + (blocksize - neblock_rem),
                destination + (blocksize - neblock_rem),
                neblock_rem,
                neblock_rem);
        }
    }
}
