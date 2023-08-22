using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace PureHDF;

// https://eli.thegreenplace.net/2015/memory-layout-of-multi-dimensional-arrays
// https://social.msdn.microsoft.com/Forums/vstudio/en-US/0c00d9af-4c10-4451-b7eb-972de0944ff8/is-systemarray-data-stored-in-rowmajor-or-columnmajor-order?forum=netfxbcl
// https://github.com/HDFGroup/HDF.PInvoke/issues/52
static partial class ArrayExtensions
{
    private static void ValidateInputData<T>(T[] data, long[] dimensionSizes)
    {
        var unspecifiedDimensions = dimensionSizes
            .Where(size => size <= 0)
            .Count();

        if (unspecifiedDimensions > 1)
        {
            throw new Exception("You may only provide a single unspecified dimension.");
        }

        else if (unspecifiedDimensions == 1)
        {
            var index = Array.FindIndex(dimensionSizes, size => size <= 0);
            long missingDimensionSize = 1;

            for (int i = 0; i < dimensionSizes.Length; i++)
            {
                if (i != index)
                    missingDimensionSize *= dimensionSizes[i];
            }

            missingDimensionSize = data.Length / missingDimensionSize;
            dimensionSizes[index] = missingDimensionSize;
        }

        var totalSize = (long)MathUtils.CalculateSize(dimensionSizes.Select(value => (ulong)value).ToArray());

        if (data.LongLength != totalSize)
            throw new Exception("The total number of elements in all dimensions of the reshaped array must be equal to the total number of elements of the current array.");
    }

#if NET6_0_OR_GREATER
        private static unsafe void CopyData<T>(Span<T> source, Array target)
        where T : unmanaged
    {
        var sourceBytes = MemoryMarshal.AsBytes(source);

        var targetSpan = MemoryMarshal.CreateSpan(
            reference: ref MemoryMarshal.GetArrayDataReference(target), 
            length: target.Length * Unsafe.SizeOf<T>());

        sourceBytes.CopyTo(targetSpan);
    }
#else
    private static unsafe void CopyData<T>(Span<T> source, void* target)
        where T : unmanaged
    {
        var sourceBytes = MemoryMarshal.AsBytes(source);
        var targetSpan = new Span<byte>(target, source.Length * Unsafe.SizeOf<T>());

        sourceBytes.CopyTo(targetSpan);
    }
#endif

}