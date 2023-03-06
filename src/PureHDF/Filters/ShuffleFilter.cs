#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

namespace PureHDF
{
    internal static class ShuffleFilter
    {
        public static unsafe void Shuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
        {
#if NET5_0_OR_GREATER
            if (Avx2.IsSupported)
                ShuffleAvx2.Shuffle(bytesOfType, source, destination);

            else if (Sse2.IsSupported)
                ShuffleSse2.Shuffle(bytesOfType, source, destination);

            else
#endif
            ShuffleGeneric.Shuffle(bytesOfType, source, destination);
        }

        public static unsafe void Unshuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
        {
#if NET5_0_OR_GREATER
            if (Avx2.IsSupported)
                ShuffleAvx2.Unshuffle(bytesOfType, source, destination);

            else if (Sse2.IsSupported)
                ShuffleSse2.Unshuffle(bytesOfType, source, destination);

            else
#endif
            ShuffleGeneric.Unshuffle(bytesOfType, source, destination);
        }
    }
}