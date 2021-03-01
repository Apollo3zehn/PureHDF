using System;
using System.Collections.Generic;
using System.Linq;

#warning Workaround for https://stackoverflow.com/questions/62648189/testing-c-sharp-9-0-in-vs2019-cs0518-isexternalinit-is-not-defined-or-imported
namespace System.Runtime.CompilerServices
{
    public record IsExternalInit;
}

// see also: https://github.com/Unidata/netcdf-c/blob/master/libnczarr/zchunking.c
namespace HDF5.NET
{
    internal record SliceProjectionResult(int Dimension, SliceProjection[] SliceProjections);

    internal record Slice(ulong Start, ulong Stop, ulong Stride)
    {
        internal static Slice Create(ulong start, ulong count, ulong stride)
        {
            var stop = start + count * stride;
            return new Slice(start, stop, stride);
        }

        public ulong Count => (this.Stop - this.Start) / this.Stride;
        public ulong Length => this.Count * this.Stride;

        public void Verify()
        {
            if (this.Stop < this.Start)
                throw new Exception("'Start' must be greater or equal to 'Stop'.");

            if (this.Stride == 0)
                throw new Exception("'Stride' must be greater than zero.");
        }
    }

    internal record SliceProjection(ulong ChunkIndex, Slice ChunkSlice, Slice MemorySlice);
    internal record HyperslabSettings(int Rank, ulong[] DatasetDims, ulong[] ChunkDims, ulong[] MemoryDims);

    internal record CopyInfo(int Rank, ulong[] NormalizedDatasetDims, ulong[] ChunkDims, SliceProjectionResult[] SliceProjectionResults, int TypeSize, Memory<byte>[] Chunks, Memory<byte> Target);

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

        public SliceProjectionResult[] ComputeSliceProjections(Slice[] datasetSlices, Slice[] memorySlices, HyperslabSettings settings)
        {
            var rank = settings.Rank;

            if (settings.ChunkDims.Length != rank ||
                settings.DatasetDims.Length != rank ||
                settings.MemoryDims.Length != rank ||
                datasetSlices.Length != rank)
                throw new Exception($"The chunk, dataset, memory and slice array dimensions must match the rank of '{rank}'.");

            return Enumerable.Range(0, settings.Rank)
                .Select(dimension =>
                {
                    var slice = datasetSlices[dimension];
                    var datasetLength = settings.DatasetDims[dimension];

                    // validate input data
                    slice.Verify();

                    if ((slice.Stop - slice.Stride) >= datasetLength)
                        throw new Exception("The slice exceeds the dataset size.");

                    // compute projections
                    var projections = this
                        .ComputeProjectionsPerSlice(settings, dimension, slice)
                        .ToArray();

                    return new SliceProjectionResult(dimension, projections);
                })
                .ToArray();
        }

        private SliceProjection[] ComputeProjectionsPerSlice(HyperslabSettings settings, int dimension, Slice datasetSlice)
        {
            // prepare projections list
            var chunkLength = settings.ChunkDims[dimension];
            var firstChunk = H5Utils.FloorDiv(datasetSlice.Start, chunkLength);
            var lastChunk = H5Utils.CeilDiv(datasetSlice.Stop, chunkLength);
            var count = (int)(lastChunk - firstChunk);
            var projections = new List<SliceProjection>(capacity: count);

            // loop over the full range
            var chunkIndex = firstChunk;
            var isFirst = true;

            while (true)
            {
                // get projection
                var projection = this.ComputeProjection(isFirst, dimension, chunkIndex, datasetSlice, settings);
                projections.Add(projection);

                // determine next chunk
                var absoluteStop = chunkLength * chunkIndex + projection.ChunkSlice.Stop;
                var absoluteNext = absoluteStop + datasetSlice.Stride;

                if (absoluteNext > settings.DatasetDims[dimension])
                    break;

                chunkIndex = H5Utils.FloorDiv(absoluteNext, chunkLength);
                
                // clean up
                isFirst = false;
            }

            return projections.ToArray();
        }

        // Assumption: It is guaranteed that this chunk contains at least a single point
        private SliceProjection ComputeProjection(bool isFirst, int dimension, ulong chunkIndex, Slice datasetSlice, HyperslabSettings settings)
        {
            var datasetLength = settings.DatasetDims[dimension];
            var chunkLength = settings.ChunkDims[dimension];

            /* calculate optimized start, i.e position of first point in chunk */
            ulong optimizedStart;

            if (isFirst)
            {
                optimizedStart = datasetSlice.Start % chunkLength;
            }
            else if (datasetSlice.Stride == 1)
            {
                optimizedStart = 0;
            }
            else
            {
                var absoluteStart = chunkIndex * chunkLength;
                var startDistance = absoluteStart - datasetSlice.Start;
                var consumedStartStride = startDistance % datasetSlice.Stride;

                optimizedStart = (datasetSlice.Stride - consumedStartStride) % datasetSlice.Stride;
            }

            /* calculate optimized stop, i.e. position of last point in chunk + 1 */
            var absoluteStop = Math.Min((chunkIndex + 1) * chunkLength, datasetLength);
            var stopDistance = absoluteStop - datasetSlice.Start;

            var consumedStopStride = stopDistance % datasetSlice.Stride;

            if (consumedStopStride == 0)
                consumedStopStride = datasetSlice.Stride;

            var relativeStop = absoluteStop % chunkLength;

            if (relativeStop == 0)
                relativeStop = chunkLength;

            var optimizedStop = relativeStop - consumedStopStride + 1;

            /* collect & return */
            var chunkSlice = new Slice(optimizedStart, optimizedStop, datasetSlice.Stride);
            var memorySlice = new Slice(0, 0, 0);

            return new SliceProjection(chunkIndex, chunkSlice, memorySlice);
        }      

        #endregion
    }
}

