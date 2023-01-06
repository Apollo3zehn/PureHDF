using System.Runtime.InteropServices;

namespace HDF5.NET
{
    // https://eli.thegreenplace.net/2015/memory-layout-of-multi-dimensional-arrays
    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/0c00d9af-4c10-4451-b7eb-972de0944ff8/is-systemarray-data-stored-in-rowmajor-or-columnmajor-order?forum=netfxbcl
    // https://github.com/HDFGroup/HDF.PInvoke/issues/52
    static partial class ArrayExtensions
    {
        private static void ValidateInputData<T>(T[] data, long[] dimensionSizes)
        {
            // sanity checks
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
        }

        private static unsafe void CopyData<T>(T[] source, void* target)
            where T : unmanaged
        {
// TODO: Unsafe.CopyBlock

            var sourceBytes = MemoryMarshal.AsBytes(source.AsSpan());
            var bytePtr = (byte*)target;

            for (int i = 0; i < sourceBytes.Length; i++)
            {
                bytePtr[i] = sourceBytes[i];
            }
        }
    }
}
