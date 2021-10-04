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

            this.ElementCount = this.Rank > 0
                ? elementCount
                : 0;
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

        public static HyperslabSelection Scalar()
        {
            return new HyperslabSelection(0, 1);
        }

        public override ulong ElementCount { get; }

        #region IEnumerable

        public override IEnumerator<Slice> GetEnumerator()
        {
            return this.Walk();
        }

        #endregion
    }
}