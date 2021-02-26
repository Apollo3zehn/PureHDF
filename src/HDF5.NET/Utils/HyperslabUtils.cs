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
        public ulong ChunkIndex { get; set; }
        public Slice ChunkSlice { get; set; }
        public Slice MemorySlice { get; set; }
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
                HyperslabUtils.CopyChunkData(copyInfo, dimension: 0, sourceStarts, targetStarts, chunkProjections.ToArray());
            }
        }

        private static void CopyChunkData(CopyInfo copyInfo, int dimension, ulong[] sourceStarts, ulong[] targetStarts, SliceProjection[] chunkProjections)
        {
            // go deeper (only until second last dimension to avoid too many function calls)
            if (dimension < copyInfo.Rank - 1)
            {
                var projection = chunkProjections[dimension];
                var sourceStart = projection.ChunkSlice.Start;
                var targetStart = projection.MemorySlice.Start;

                for (ulong i = 0; i < projection.MemorySlice.Length; i++)
                {
                    sourceStarts[dimension] = sourceStart;
                    targetStarts[dimension] = targetStart;
                    HyperslabUtils.CopyChunkData(copyInfo, dimension + 1, sourceStarts, targetStarts, chunkProjections);
                    sourceStart += projection.ChunkSlice.Stride;
                    targetStart += projection.MemorySlice.Stride;
                }
            }
            // copy
            else
            {
#warning necessary because loop above ends early .. 
                var projection = chunkProjections[copyInfo.Rank - 1];
                sourceStarts[copyInfo.Rank - 1] = projection.ChunkSlice.Start;
                targetStarts[copyInfo.Rank - 1] = projection.MemorySlice.Start;

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
                var chunk = copyInfo
                    .Chunks[chunkIndex];

                var lastProjection = chunkProjections.Last();

                // bulk copy
                if (lastProjection.ChunkSlice.Stride == 1)
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
    }

    internal class Hyperslabber
    {
        #region Fields

        private int counter = 0;

        #endregion

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
                        .Select(projection => new SliceProjection() 
                        { 
                            ChunkIndex = projection.ChunkIndex, 
                            ChunkSlice = projection.ChunkSlice, 
                            MemorySlice = projection.MemorySlice
                        })
                        .ToArray();

                    return new SliceProjectionResult(dimension, projections);
                })
                .ToArray();
        }

        private void ComputeProjections(HyperslabSettings settings, int dimension, ulong chunkIndex, Slice slice, uint n, InternalSliceProjection[] projections)
        {
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
            var abslimit = (chunkIndex + 1) * chunkDims;

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
                projection.IOPos = H5Utils.CeilDiv(projection.Offset - slice.Start, slice.Stride);
            }

            if (slice.Stop > abslimit)
                projection.Stop = chunkDims;
            else
                projection.Stop = slice.Stop - projection.Offset;

            projection.IOCount = H5Utils.CeilDiv(projection.Stop - projection.First, slice.Stride);

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

            this.VerifySlice(projection.MemorySlice);
            this.VerifySlice(projection.ChunkSlice);
        }

        private void SkipChunk(Slice slice, InternalSliceProjection projection)
        {
            projection.Skip = true;
            projection.First = 0;
            projection.Last = 0;
            projection.IOPos = H5Utils.CeilDiv(projection.Offset - slice.Start, slice.Stride);
            projection.IOCount = 0;
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

