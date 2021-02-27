using System;
using System.Collections.Generic;
using System.Linq;

#warning Workaround for https://stackoverflow.com/questions/62648189/testing-c-sharp-9-0-in-vs2019-cs0518-isexternalinit-is-not-defined-or-imported
namespace System.Runtime.CompilerServices
{
    public record IsExternalInit;
}

// based on https://github.com/Unidata/netcdf-c/blob/master/libnczarr/zchunking.c
namespace HDF5.NET
{
    internal record ChunkRange(ulong Start, ulong Stop);
    internal record SliceProjectionResult(int Dimension, SliceProjection[] SliceProjections);

    internal record Slice
    {
        public ulong Start { get; set; }
        public ulong Stop { get; set; }
        public ulong Stride { get; set; }
        public ulong Length => H5Utils.CeilDiv(this.Stop - this.Start, this.Stride);
    }

    internal class SliceProjection
    {
        public ulong ChunkIndex { get; set; }
        public Slice ChunkSlice { get; set; }
        public Slice MemorySlice { get; set; }

        internal bool Skip { get; set; }
        internal ulong Last { get; set; }
        internal ulong Offset { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as SliceProjection;

            if (other != null &&
                this.ChunkIndex.Equals(other.ChunkIndex) &&
                this.ChunkSlice.Equals(other.ChunkSlice) &&
                this.MemorySlice.Equals(other.MemorySlice))
                return true;

            else
                return false;
        }

        public override int GetHashCode()
        {
            return 
                1 * this.ChunkIndex.GetHashCode() +
                3 * this.ChunkSlice.GetHashCode() +
                5 * this.MemorySlice.GetHashCode();
        }
    }

    internal record HyperslabSettings(int Rank, ulong[] DatasetDims, ulong[] ChunkDims);

    internal record CopyInfo(int Rank, ulong[] NormalizedDatasetDims, ulong[] ChunkDims, SliceProjectionResult[] SliceProjectionResults, int TypeSize, Memory<byte>[] Chunks, Memory<byte> Target);

#warning everything to (u)int? because chunk size is limited? but target array is not limited .... should work anyway
    internal static class HyperslabUtils
    {
        public static void Copy(CopyInfo copyInfo)
        {
            var allProjections = copyInfo.SliceProjectionResults.Select(result => result.SliceProjections);
            var sourceStarts = new ulong[copyInfo.Rank];
            var targetStarts = new ulong[copyInfo.Rank];

            foreach (var chunkProjections in H5Utils.CartesianProduct(allProjections))
            {
                HyperslabUtils.ProcessProjections(copyInfo, dimension: 0, sourceStarts, targetStarts, chunkProjections.ToArray());
            }
        }

        private static void ProcessProjections(CopyInfo copyInfo, int dimension, ulong[] sourceStarts, ulong[] targetStarts, SliceProjection[] chunkProjections)
        {
            // This method is called recursively until source and target
            // starts array is filled. Then the actual data are copied.

            var projection = chunkProjections[dimension];
            sourceStarts[dimension] = projection.ChunkSlice.Start;
            targetStarts[dimension] = projection.MemorySlice.Start;

            // track progress for all except the last dimension
            if (dimension < copyInfo.Rank - 1)
            {
                for (ulong i = 0; i < projection.ChunkSlice.Length; i++)
                {
                    HyperslabUtils.ProcessProjections(copyInfo, dimension + 1, sourceStarts, targetStarts, chunkProjections);
                    sourceStarts[dimension] += projection.ChunkSlice.Stride;
                    targetStarts[dimension] += projection.MemorySlice.Stride;
                }
            }
            // copy data
            else
            {
                HyperslabUtils.CopyChunkData(copyInfo, sourceStarts, targetStarts, chunkProjections);
            }
        }

        private static void CopyChunkData(CopyInfo copyInfo, ulong[] sourceStarts, ulong[] targetStarts, SliceProjection[] chunkProjections)
        {
            // linear indices
            var chunkIndex = 0UL;
            var sourceOffset = 0UL;
            var targetOffset = 0UL;

            for (int i = 0; i < copyInfo.Rank; i++)
            {
#warning pull chunkIndex calculation out of this function
                chunkIndex = chunkIndex * copyInfo.NormalizedDatasetDims[i] + chunkProjections[i].ChunkIndex;
                sourceOffset = sourceOffset * copyInfo.ChunkDims[i] + sourceStarts[i];
                targetOffset = targetOffset * copyInfo.ChunkDims[i] + targetStarts[i];
            }

            // find chunk and last projection
            var chunk = copyInfo.Chunks[chunkIndex];
            var lastProjection = chunkProjections.Last();

            // bulk copy
            if (lastProjection.ChunkSlice.Stride == 1 
             && lastProjection.MemorySlice.Stride == 1)
            {
                var length = lastProjection.MemorySlice.Length;
                var source = chunk.Slice((int)sourceOffset * copyInfo.TypeSize, (int)length * copyInfo.TypeSize);
                var target = copyInfo.Target.Slice((int)targetOffset * copyInfo.TypeSize);

                source.CopyTo(target);
            }
            // for loop
            else
            {
                var currentSourceOffset = sourceOffset;
                var target = copyInfo.Target.Slice((int)targetOffset);

                for (ulong i = 0; i < lastProjection.MemorySlice.Length; i++)
                {
                    var source = chunk.Slice((int)currentSourceOffset * copyInfo.TypeSize, copyInfo.TypeSize);
                    source.CopyTo(target);
                    target = target.Slice(copyInfo.TypeSize);
                    currentSourceOffset += lastProjection.ChunkSlice.Stride;
                }
            }
        }
    }

    internal class Hyperslabber
    {
        #region Methods

        public SliceProjectionResult[] ComputeSliceProjections(HyperslabSettings settings, Slice[] slices)
        {
            return Enumerable.Range(0, settings.Rank)
                .Select(dimension =>
                {
                    var slice = slices[dimension];
                    var datasetDim = settings.DatasetDims[dimension];
                    var chunkDim = settings.ChunkDims[dimension];

                    // validate input data
                    this.VerifySlice(slice);

                    if (slice.Stop > datasetDim)
                        throw new Exception("The slice exceeds the dataset size.");

                    // calculate touched chunk range
                    var start = H5Utils.FloorDiv(slice.Start, chunkDim);
                    var stop = H5Utils.CeilDiv(slice.Stop, chunkDim);
                    var range = new ChunkRange(start, stop);

                    // calculate projections
                    var projections = this
                        .ComputeProjectionsPerSlice(settings, dimension, slice, range)
                        .Where(projection => !projection.Skip)
                        .ToArray();

                    return new SliceProjectionResult(dimension, projections);
                })
                .ToArray();
        }

        private void ComputeProjections(HyperslabSettings settings, int dimension, ulong chunkIndex, Slice slice, uint n, SliceProjection[] projections)
        {
            SliceProjection previous = null;

            var datasetDims = settings.DatasetDims[dimension]; /* the dimension length for r'th dimension */
            var chunkDims = settings.ChunkDims[dimension]; /* the chunk length corresponding to the dimension */

            if (projections[n] == null)
                projections[n] = new SliceProjection();

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

            projection.ChunkIndex = chunkIndex;
            projection.Offset = chunkDims * chunkIndex; /* with respect to dimension (WRD) */

            /* limit in the n'th touched chunk, taking dimlen and stride->stop into account. */
            var abslimit = (chunkIndex + 1) * chunkDims;

            if (abslimit > slice.Stop) 
                abslimit = slice.Stop;

            if (abslimit > datasetDims) 
                abslimit = datasetDims;

            var limit = abslimit - projection.Offset;

            /* See if the next point after the last one in prev lands in the current projection.
            If not, then we have skipped the current chunk. Also take limit into account.
            Note by definition, n must be greater than zero because we always start in a relevant chunk.
            */

            ulong ioPos;
            ulong first;

            if (n == 0)
            {
                /* initial case: original slice start is in 1st projection */
                first = slice.Start - projection.Offset;
                ioPos = 0;
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
                absthislast = projection.Offset + limit;

                /* Compute next point touched after the last point touched in previous projection;
                note that the previous projection might be wrt a chunk other than the immediately preceding
                one (because the intermediate ones were skipped).
                */
                absnextpoint = abslastpoint + slice.Stride; /* abs next point to be touched */

                if (absnextpoint >= absthislast)
                {
                    /* this chunk is being skipped */
                    projection.Skip = true;
                    return;
                }

                /* Compute start point in this chunk */
                /* basically absnextpoint - abs start of this projection */
                first = absnextpoint - projection.Offset;

                /* Compute the memory location of this first point in this chunk */
                ioPos = H5Utils.CeilDiv(projection.Offset - slice.Start, slice.Stride);
            }

            ulong stop;

            if (slice.Stop > abslimit)
                stop = chunkDims;
            else
                stop = slice.Stop - projection.Offset;

            /* Compute the slice relative to this chunk. Recall the possibility that start + stride >= limit */
            projection.ChunkSlice = new Slice()
            {
                Start = first,
                Stop = stop,
                Stride = slice.Stride,
            };

            this.VerifySlice(projection.ChunkSlice);

            /* Last place to be touched */
            var ioCount = H5Utils.CeilDiv(stop - first, slice.Stride);
            projection.Last = first + (slice.Stride * (ioCount - 1));

            projection.MemorySlice = new Slice()
            {
                Start = ioPos,
                Stop = ioPos + ioCount,
                Stride = 1,
            };

            this.VerifySlice(projection.MemorySlice);
        }

        private SliceProjection[] ComputeProjectionsPerSlice(HyperslabSettings settings, int dimension, Slice slice, ChunkRange range)
        {
            /* Part fill the Slice Projections */
            var count = (uint)(range.Stop - range.Start);
            var projections = new SliceProjection[count];

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

        private void VerifySlice(Slice slice)
        {
            if (slice.Stop < slice.Start)
                throw new Exception("'Slice.Start' must be greater or equal to 'slice.Stop'.");

            if (slice.Stride == 0)
                throw new Exception("'Slice.Stride' must be greater than zero.");
        }

        #endregion
    }
}

