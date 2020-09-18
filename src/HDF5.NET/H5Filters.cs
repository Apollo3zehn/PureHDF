using System;

namespace HDF5.NET
{
    internal static class H5Filters
    {
        public static unsafe void Shuffle(uint flags, Span<byte> source, uint[] parameters)
        {
            // H5Z_shuffle.c (H5Z_filter_shuffle)

            /* Check arguments */
            if (parameters.Length != 1 || parameters[0] == 0)
                throw new Exception("Invalid shuffle parameters.");

            fixed (byte* sourcePtr = source)
            {
                /* Get the number of bytes per element from the parameter block */
                var elementSize = parameters[0];

                /* Compute the number of elements in buffer */
                var elementCount = source.Length / elementSize;

                /* Don't do anything for 1-byte elements, or "fractional" elements */
                if (elementSize > 1 && elementCount > 1)
                {
                    /* Compute the leftover bytes if there are any */
                    var leftOver = source.Length % elementSize;

                    /* Allocate the destination buffer */
                    var target = new byte[source.Length];

                    //fixed (byte* targetPtr = target)
                    //{
                    //    if (flags)
                    //}
                }
            }
        }
    }
}
