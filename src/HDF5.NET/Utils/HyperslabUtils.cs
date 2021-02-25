using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

#warning Workaround for https://stackoverflow.com/questions/62648189/testing-c-sharp-9-0-in-vs2019-cs0518-isexternalinit-is-not-defined-or-imported
namespace System.Runtime.CompilerServices
{
    public record IsExternalInit;
}

// based on https://github.com/Unidata/netcdf-c/blob/master/libnczarr/zchunking.c
namespace HDF5.NET
{
    internal record ChunkRange(ulong Start, ulong Stop);
    internal record SliceProjectionResult(int Rank, SliceProjection[] SliceProjections);

    internal record Slice
    {
        public ulong Start { get; set; }
        public ulong Stop { get; set; }
        public ulong Stride { get; set; }
    }

    internal record InternalSliceProjection
    {
        public int Id { get; set; }
        public bool Skip { get; set; }
        public ulong ChunkIndex { get; set; }
        public ulong Offset { get; set; }
        public ulong First { get; set; }
        public ulong Last { get; set; }
        public ulong Stop { get; set; }
        public ulong Limit { get; set; }
        public ulong IOPos { get; set; }
        public ulong IOCount { get; set; }
        public Slice ChunkSlice { get; set; }
        public Slice MemorySlice { get; set; }
    }

    internal record SliceProjection
    {
        public int Id { get; set; }
        public ulong ChunkIndex { get; set; }
        public Slice ChunkSlice { get; set; }
        public Slice MemorySlice { get; set; }
    }

    internal record HyperslabSettings(int Rank, ulong[] DatasetDims, ulong[] ChunkDims, ulong[] MemoryShape);

    internal class Hyperslabber
    {
        #region Fields

        private int counter = 0;

        #endregion

        #region Methods

        public List<SliceProjectionResult> ComputeSliceProjections(HyperslabSettings settings, Slice[] slices)
        {
            var ranges = this.ComputeChunkRanges(settings.Rank, slices, settings.ChunkDims);

            return Enumerable.Range(0, settings.Rank)
                .Select(dimension =>
                {
                    var projections = this
                        .ComputeProjectionsPerSlice(settings, dimension, slices[dimension], ranges[dimension])
                        .Select(projection => new SliceProjection() 
                        { 
                            Id = projection.Id, 
                            ChunkIndex = projection.ChunkIndex, 
                            ChunkSlice = projection.ChunkSlice, 
                            MemorySlice = projection.MemorySlice 
                        })
                        .ToArray();

                    return new SliceProjectionResult(dimension, projections);
                })
                .ToList();
        }

        private ChunkRange[] ComputeChunkRanges(int rank, Slice[] slices, ulong[] chunkDims)
        {
            return Enumerable.Range(0, rank)
                .Select(dimension => this.ComputerIntersection(slices[dimension], chunkDims[dimension]))
                .ToArray();
        }

        private ChunkRange ComputerIntersection(Slice slice, ulong chunkDims)
        {
            var start = this.FloorDiv(slice.Start, chunkDims);
            var stop = this.CeilDiv(slice.Stop, chunkDims);

            return new ChunkRange(start, stop);
        }

        private void ComputeProjections(HyperslabSettings settings, int dimension, ulong chunkIndex, Slice slice, uint n, InternalSliceProjection[] projections)
        {
            ulong abslimit;
            InternalSliceProjection previous = null;

            var datasetDims = settings.DatasetDims[dimension]; /* the dimension length for r'th dimension */
            var chunkDims = settings.ChunkDims[dimension]; /* the chunk length corresponding to the dimension */

            if (projections[n] == null)
                projections[n] = new InternalSliceProjection();

            var projection = projections[n];

            if (n > 0)
            {
                /* Find last non-skipped projection */
                for (var i = n - 1; i >= 0; i--) /* walk backward */
                {
                    if (!projections[i].Skip)
                    {
                        previous = projections[i];
                        break;
                    }
                }

                if (previous == null)
                    throw new Exception("Unable to find previous projection.");
            }

            projection.Id = ++counter;
            projection.ChunkIndex = chunkIndex;
            projection.Offset = chunkDims * chunkIndex; /* with respect to dimension (WRD) */

            /* limit in the n'th touched chunk, taking dimlen and stride->stop into account. */
            abslimit = (chunkIndex + 1) * chunkDims;

            if (abslimit > slice.Stop) 
                abslimit = slice.Stop;

            if (abslimit > datasetDims) 
                abslimit = datasetDims;

            projection.Limit = abslimit - projection.Offset;

            /* See if the next point after the last one in prev lands in the current projection.
            If not, then we have skipped the current chunk. Also take limit into account.
            Note by definition, n must be greater than zero because we always start in a relevant chunk.
            */

            if (n == 0)
            {
                /* initial case: original slice start is in 1st projection */
                projection.First = slice.Start - projection.Offset;
                projection.IOPos = 0;
            }
            else /* n > 0 */
            {
                if (previous == null)
                    throw new Exception("Unable to find previous projection.");

                /* Use absolute offsets for these computations to avoid negative values */
                ulong abslastpoint, absnextpoint, absthislast;

                /* abs last point touched in prev projection */
                abslastpoint = previous.Offset + previous.Last;

                /* Compute the abs last touchable point in this chunk */
                absthislast = projection.Offset + projection.Limit;

                /* Compute next point touched after the last point touched in previous projection;
                note that the previous projection might be wrt a chunk other than the immediately preceding
                one (because the intermediate ones were skipped).
                */
                absnextpoint = abslastpoint + slice.Stride; /* abs next point to be touched */

                if (absnextpoint >= absthislast)
                {
                    /* this chunk is being skipped */
                    this.SkipChunk(slice, projection);
                    return;
                }

                /* Compute start point in this chunk */
                /* basically absnextpoint - abs start of this projection */
                projection.First = absnextpoint - projection.Offset;

                /* Compute the memory location of this first point in this chunk */
                projection.IOPos = this.CeilDiv((projection.Offset - slice.Start), slice.Stride);
            }

            if (slice.Stop > abslimit)
                projection.Stop = chunkDims;
            else
                projection.Stop = slice.Stop - projection.Offset;

            projection.IOCount = this.CeilDiv(projection.Stop - projection.First, slice.Stride);

            /* Compute the slice relative to this chunk. Recall the possibility that start + stride >= projection.Limit */
            projection.ChunkSlice = new Slice()
            {
                Start = projection.First,
                Stop = projection.Stop,
                Stride = slice.Stride,
            };

            /* Last place to be touched */
            projection.Last = projection.First + (slice.Stride * (projection.IOCount - 1));

            projection.MemorySlice = new Slice()
            {
                Start = projection.IOPos,
                Stop = projection.IOPos + projection.IOCount,
                Stride = 1,
            };

            if (!this.VerifySlice(projection.MemorySlice) || !this.VerifySlice(projection.ChunkSlice))
                throw new Exception("Slice is invalid.");
        }

        private void SkipChunk(Slice slice, InternalSliceProjection projection)
        {
            projection.Skip = true;
            projection.First = 0;
            projection.Last = 0;
            projection.IOPos = CeilDiv(projection.Offset - slice.Start, slice.Stride);
            projection.IOCount = 0;
            projection.ChunkSlice.Start = 0;
            projection.ChunkSlice.Stop = 0;
            projection.ChunkSlice.Stride = 1;
            projection.MemorySlice.Start = 0;
            projection.MemorySlice.Stop = 0;
            projection.MemorySlice.Stride = 1;
        }

        private InternalSliceProjection[] ComputeProjectionsPerSlice(HyperslabSettings settings, int dimension, Slice slice, ChunkRange range)
        {
            /* Part fill the Slice Projections */
            var count = (uint)(range.Stop - range.Start);
            var projections = new InternalSliceProjection[count];

            /* Iterate over each chunk that intersects slice to produce projection */
            uint n;
            ulong chunkIndex;

            for (n = 0, chunkIndex = range.Start; chunkIndex < range.Stop; chunkIndex++, n++)
            {
                this.ComputeProjections(settings, dimension, chunkIndex, slice, n, projections);
            }

            return projections;
        }

        #endregion

        #region Utils

        private bool VerifySlice(Slice slice)
        {
            if (slice.Stop < slice.Start)
                return false;

            if (slice.Stride <= 0)
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong FloorDiv(ulong x, ulong y)
        {
            return x / y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong CeilDiv(ulong x, ulong y)
        {
            return x % y == 0 ? x / y : x / y + 1;
        }

        #endregion
    }
}

