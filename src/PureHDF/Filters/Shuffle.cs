#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

namespace PureHDF.Filters;

internal static class Shuffle
{
    public static unsafe void DoShuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
#if NET6_0_OR_GREATER
        if (Avx2.IsSupported)
            ShuffleAvx2.DoShuffle(bytesOfType, source, destination);

        else if (Sse2.IsSupported)
            ShuffleSse2.DoShuffle(bytesOfType, source, destination);

        else
#endif
        ShuffleGeneric.DoShuffle(bytesOfType, source, destination);
    }

    public static unsafe void DoUnshuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
#if NET6_0_OR_GREATER
        if (Avx2.IsSupported)
            ShuffleAvx2.DoUnshuffle(bytesOfType, source, destination);

        else if (Sse2.IsSupported)
            ShuffleSse2.DoUnshuffle(bytesOfType, source, destination);

        else
#endif
        ShuffleGeneric.DoUnshuffle(bytesOfType, source, destination);
    }
}