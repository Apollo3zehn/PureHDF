using System;
using System.Collections.Generic;
using System.Linq;

namespace HDF5.NET
{
    public partial class HyperslabSelection : Selection
    {
        public HyperslabSelection(ulong start, ulong block)
            : this(rank: 1, new ulong[] { start }, new ulong[] { block })
        {
            //
        }

        public HyperslabSelection(ulong start, ulong stride, ulong count, ulong block)
            : this(rank: 1, new ulong[] { start }, new ulong[] { stride }, new ulong[] { count }, new ulong[] { block })
        {
            //
        }

        public HyperslabSelection(int rank, ulong[] starts, ulong[] blocks)
            : this(rank, starts, blocks, Enumerable.Repeat(1UL, rank).ToArray(), blocks)
        {
            //
        }

        public HyperslabSelection(int rank, ulong[] starts, ulong[] strides, ulong[] counts, ulong[] blocks)
        {
            if (starts.Length != rank || strides.Length != rank || counts.Length != rank || blocks.Length != rank)
                throw new RankException($"The start, stride, count, and block arrays must be the same size as the rank '{rank}'.");

            this.Rank = rank;
            this.StartsField = starts.ToArray();
            this.StridesField = strides.ToArray();
            this.CountsField = counts.ToArray();
            this.BlocksField = blocks.ToArray();

            for (int i = 0; i < this.Rank; i++)
            {
                if (this.StridesField[i] == 0)
                    throw new ArgumentException("Stride must be > 0.");

                if (this.StridesField[i] < this.BlocksField[i])
                    throw new ArgumentException("Stride must be >= block.");
            }

            /* count */
            var elementCount = 1UL;

            for (int i = 0; i < this.Rank; i++)
            {
                elementCount *= this.Counts[i] * this.Blocks[i];
            }

            this.TotalElementCount = this.Rank > 0
                ? elementCount
                : 0;
        }

        public int Rank { get; }

        public IReadOnlyList<ulong> Starts => StartsField;

        public IReadOnlyList<ulong> Strides => StridesField;

        public IReadOnlyList<ulong> Counts => CountsField;

        public IReadOnlyList<ulong> Blocks => BlocksField;

        public override ulong TotalElementCount { get; }

        public override IEnumerable<Step> Walk(ulong[] limits)
        {
            /* Validate arrays */
            if (limits.Length != this.Rank)
                throw new RankException("The length of the limits parameter must match this hyperslab's rank.");

            for (int dimension = 0; dimension < this.Rank; dimension++)
            {
                if (this.GetStop(dimension) > limits[dimension])
                    throw new ArgumentException("The selection exceeds the limits.");
            }

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
            var step = new Step() { Coordinates = offsets.ToArray() };

            /* loop until all data have been processed */
            while (true)
            {
                /* compute number of consecutive points in current slice */
                ulong totalLength;

                if (supportsBulkCopy)
                    totalLength = lastDimStop - offsets[lastDim];

                else
                    totalLength = lastDimBlock;

                /* return next step */
                for (int i = 0; i < offsets.Length; i++)
                {
                    step.Coordinates[i] = offsets[i];
                }

                step.ElementCount = totalLength;

                yield return step;

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