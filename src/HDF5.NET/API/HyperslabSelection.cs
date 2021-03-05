using System;
using System.Collections.Generic;
using System.Linq;

namespace HDF5.NET
{
    public class HyperslabSelection : Selection
    {
        internal ulong[] StartsField;
        internal ulong[] StridesField;
        internal ulong[] CountsField;
        internal ulong[] BlocksField;

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
        }

        public int Rank { get; }

        public IReadOnlyList<ulong> Starts => StartsField;

        public IReadOnlyList<ulong> Strides => StridesField;

        public IReadOnlyList<ulong> Counts => CountsField;

        public IReadOnlyList<ulong> Blocks => BlocksField;

        public static HyperslabSelection All(ulong[] dims)
        {
            var start = dims.ToArray();
            start.AsSpan().Fill(0);

            var block = dims;

            return new HyperslabSelection(rank: dims.Length, start, block);
        }

        public ulong GetTotalCount()
        {
            var totalCount = 1UL;

            for (int i = 0; i < this.Rank; i++)
            {
                totalCount *= this.Counts[i] * this.Blocks[i];
            }

            return totalCount;
        }

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
    }
}