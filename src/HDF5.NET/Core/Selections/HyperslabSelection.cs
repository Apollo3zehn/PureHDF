using System.Collections.Generic;
using System.Linq;

namespace HDF5.NET
{
    partial class HyperslabSelection : Selection
    {
        internal ulong[] StartsField;
        internal ulong[] StridesField;
        internal ulong[] CountsField;
        internal ulong[] BlocksField;

        internal ulong GetStop(int dimension)
        {
            // prevent underflow of ulong
            if (this.Counts[dimension] == 0)
            {
                return 0;
            }
            else
            {
                return
                    this.Starts[dimension] +
                    this.Counts[dimension] * this.Strides[dimension] -
                    (this.Strides[dimension] - this.Blocks[dimension]);
            }
        }

        private IEnumerator<Slice> Walk()
        {
            /* prepare some useful arrays */
            var lastDim = this.Rank - 1;
            var offsets = new ulong[this.Rank];
            var stops = new ulong[this.Rank];
            var strides = new ulong[this.Rank];
            var blocks = new ulong[this.Rank];
            var gaps = new ulong[this.Rank];

            for (int dimension = 0; dimension < this.Rank; dimension++)
            {
                offsets[dimension] = this.Starts[dimension];
                stops[dimension] = this.GetStop(dimension);
                strides[dimension] = this.Strides[dimension];
                blocks[dimension] = this.Blocks[dimension];
                gaps[dimension] = strides[dimension] - blocks[dimension];
            }

            /* prepare last dimension variables */
            var lastDimStop = stops[lastDim];
            var lastDimBlock = blocks[lastDim];
            var lastDimGap = gaps[lastDim];
            var supportsBulkCopy = lastDimGap == 0;
            var slice = new Slice() { Coordinates = offsets.ToArray() };

            /* loop until all data have been processed */
            while (true)
            {
                /* compute number of consecutive points in current slice */
                ulong totalLength;

                if (supportsBulkCopy)
                    totalLength = lastDimStop - offsets[lastDim];

                else
                    totalLength = lastDimBlock;

                /* return next slice */
                for (int i = 0; i < offsets.Length; i++)
                {
                    slice.Coordinates[i] = offsets[i];
                }

                slice.Length = totalLength;

                yield return slice;

                /* update offsets array */
                offsets[lastDim] += totalLength + lastDimGap;

                /* iterate backwards through all dimensions */
                for (int dimension = lastDim; dimension >= 0; dimension--)
                {
                    if (dimension != lastDim)
                    {
                        /* go one step forward */
                        offsets[dimension] += 1;

                        /* if we have reached a gap, skip that gap */
                        var consumedStride = (offsets[dimension] - this.Starts[dimension]) % strides[dimension];

                        if (consumedStride == blocks[dimension])
                            offsets[dimension] += gaps[dimension];
                    }

                    /* if the current slice is fully processed */
                    if (offsets[dimension] >= stops[dimension])
                    {
                        /* if there is more to process, reset the offset and 
                         * repeat the loop for the next higher dimension */
                        if (dimension > 0)
                            offsets[dimension] = this.Starts[dimension];

                        /* else, we are done! */
                        else
                            yield break;
                    }

                    /* otherwise, break the loop */
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}