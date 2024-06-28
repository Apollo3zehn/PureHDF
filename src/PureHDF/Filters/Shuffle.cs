using System.Runtime.Intrinsics.X86;

namespace PureHDF.Filters;

internal static class Shuffle
{
    public static unsafe void DoShuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        if (Avx2.IsSupported)
            ShuffleAvx2.DoShuffle(bytesOfType, source, destination);

        else if (Sse2.IsSupported)
            ShuffleSse2.DoShuffle(bytesOfType, source, destination);

        else
            ShuffleGeneric.DoShuffle(bytesOfType, source, destination);
    }

    public static unsafe void DoUnshuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        if (Avx2.IsSupported)
            ShuffleAvx2.DoUnshuffle(bytesOfType, source, destination);

        else if (Sse2.IsSupported)
            ShuffleSse2.DoUnshuffle(bytesOfType, source, destination);

        else
            ShuffleGeneric.DoUnshuffle(bytesOfType, source, destination);
    }
}