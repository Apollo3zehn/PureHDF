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
    internal record SliceProjection(ulong ChunkIndex, Slice ChunkSlice);

    internal record CopyInfo
        (int Rank, 
        SliceProjectionResult[] SliceProjectionResults, 
        Slice[] DatasetSlices,
        Slice[] MemorySlices,
        ulong[] DatasetDims,
        ulong[] ChunkDims,
        ulong[] MemoryDims,
        Memory<byte>[] ChunkBuffers,
        Memory<byte> MemoryBuffer,
        int TypeSize);

    internal record Slice(ulong Start, ulong Stop, ulong Stride)
    {
        internal static Slice Create(ulong start, ulong count, ulong stride)
        {
            var stop = start + count * stride;
            return new Slice(start, stop, stride);
        }

        public ulong Count => H5Utils.CeilDiv(this.Stop - this.Start, this.Stride);
        public ulong Length => this.Count * this.Stride;

        public void Verify()
        {
            if (this.Stop < this.Start)
                throw new Exception("'Start' must be greater or equal to 'Stop'.");

            if (this.Stride == 0)
                throw new Exception("'Stride' must be greater than zero.");
        }
    }

    internal static class HyperslabUtils
    {
        public static SliceProjectionResult[] ComputeSliceProjections(int rank, Slice[] datasetSlices, ulong[] datasetDims, ulong[] chunkDims)
        {
            if (chunkDims.Length != rank ||
                datasetDims.Length != rank ||
                datasetSlices.Length != rank)
                throw new Exception($"The chunk, dataset and slice array dimensions be of rank '{rank}'.");

            return Enumerable
                .Range(0, rank)
                .Select(dimension =>
                {
                    var datasetSlice = datasetSlices[dimension];
                    var datasetLength = datasetDims[dimension];

                    // validate input data
                    datasetSlice.Verify();

                    if ((datasetSlice.Stop - datasetSlice.Stride + 1) > datasetLength)
                        throw new Exception("The slice exceeds the dataset size.");

                    // compute projections
                    var chunkLength = chunkDims[dimension];

                    var projections = HyperslabUtils
                        .ComputeProjectionsPerSlice(chunkLength, datasetSlice)
                        .ToArray();

                    return new SliceProjectionResult(dimension, projections);
                })
                .ToArray();
        }

        private static SliceProjection[] ComputeProjectionsPerSlice(ulong chunkLength, Slice datasetSlice)
        {
            // compute absolute last point position
            var absoluteLast = HyperslabUtils.GetLast(datasetSlice.Start, datasetSlice.Stop, datasetSlice.Stride);

            // compute first chunk, last chunk and prepare projections list
            var firstChunk = H5Utils.FloorDiv(datasetSlice.Start, chunkLength);
            var lastChunk = H5Utils.CeilDiv(absoluteLast, chunkLength);
            var count = (int)(lastChunk - firstChunk);
            var projections = new List<SliceProjection>(capacity: count);

            // loop over the full range
            var chunkIndex = firstChunk;
            var isFirst = true;

            while (true)
            {
                // get projection
                var projection = HyperslabUtils.ComputeProjection(isFirst, datasetSlice, absoluteLast, chunkIndex, chunkLength);
                projections.Add(projection);

                // determine next chunk
                var absoluteStop = chunkLength * chunkIndex + projection.ChunkSlice.Stop;
                var absoluteNext = absoluteStop + datasetSlice.Stride;

                if (absoluteNext > absoluteLast)
                    break;

                chunkIndex = H5Utils.FloorDiv(absoluteNext, chunkLength);

                // clean up
                isFirst = false;
            }

            return projections.ToArray();
        }

        private static SliceProjection ComputeProjection(bool isFirst, Slice datasetSlice, ulong absoluteLast, ulong chunkIndex, ulong chunkLength)
        {
            // Assumption: It is guaranteed that the current chunk contains at least a single point

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
            var absoluteStop = Math.Min((chunkIndex + 1) * chunkLength, absoluteLast);
            var absoluteOptimizedStop = HyperslabUtils.GetLast(datasetSlice.Start, absoluteStop, datasetSlice.Stride);
            var optimizedStop = absoluteOptimizedStop % chunkLength;
 
            if (optimizedStop == 0)
                optimizedStop = chunkLength;

            /* collect & return */
            var chunkSlice = new Slice(optimizedStart, optimizedStop, datasetSlice.Stride);
            return new SliceProjection(chunkIndex, chunkSlice);
        }

        private static ulong GetLast(ulong start, ulong stop, ulong stride)
        {
            var distance = stop - start;
            var consumedStride = distance % stride;

            if (consumedStride == 0)
                consumedStride = stride;

            return stop - consumedStride + 1;
        }

        public static void Copy(CopyInfo copyInfo)
        {
            // validate input data
            if (copyInfo.SliceProjectionResults.Length != copyInfo.Rank)
                throw new Exception($"The slice projection results array dimensions must be of rank '{copyInfo.Rank}'");

            if (copyInfo.MemoryBuffer.Length != (int)copyInfo.MemoryDims.Aggregate((ulong)copyInfo.TypeSize, (x, y) => x * y))
                throw new Exception("Length of memory buffer must match the specified memory dimensions.");

            var sourceSelectionSize = copyInfo;

            // all projections
            var allProjections = copyInfo.SliceProjectionResults
                .Select(result => result.SliceProjections);

            // memory walker
            var memoryWalker = HyperslabUtils
                .Walk(copyInfo.Rank, copyInfo.MemorySlices, copyInfo.MemoryDims)
                .GetEnumerator();

            memoryWalker.MoveNext();

            var memorySlice = copyInfo.MemorySlices.Last();
            var memoryRemaining = memorySlice.Count;
            var memoryOffset = memoryWalker.Current;

            // normalized dataset dimensions
            var normalizedDatasetDims = new ulong[3];

            for (ulong i = 0; i < 3; i++)
            {
                normalizedDatasetDims[i] = H5Utils.CeilDiv(copyInfo.DatasetDims[i], copyInfo.ChunkDims[i]);
            }

            // for each combination
            foreach (var projections in H5Utils.CartesianProduct(allProjections))
            {
                var chunkIndices = projections
                    .Select(projection => projection.ChunkIndex)
                    .ToArray();

                var linearChunkIndex = chunkIndices
                    .ToLinearIndex(normalizedDatasetDims);

                var chunkBuffer = copyInfo
                    .ChunkBuffers[linearChunkIndex];

                var chunkSlices = projections
                    .Select(projection => projection.ChunkSlice)
                    .ToArray();

                var datasetWalker = HyperslabUtils
                    .Walk(copyInfo.Rank, chunkSlices, copyInfo.ChunkDims);

                HyperslabUtils.ProcessChunk(chunkBuffer, chunkSlices.Last(), datasetWalker,
                                            copyInfo.MemoryBuffer, memorySlice, memoryWalker,
                                            ref memoryRemaining,
                                            ref memoryOffset,
                                            copyInfo);
            }
        }

        private static void ProcessChunk(Memory<byte> chunkBuffer,
                                         Slice datasetSlice,
                                         IEnumerable<ulong> datasetWalker,
                                         Memory<byte> memoryBuffer,
                                         Slice memorySlice,
                                         IEnumerator<ulong> memoryWalker,
                                         ref ulong memoryRemaining,
                                         ref ulong memoryOffset,
                                         CopyInfo copyInfo)
        {
#warning This method could fail due to Span's int32 limitation.

            var chunkBufferSpan = chunkBuffer.Span;
            var memoryBufferSpan = memoryBuffer.Span;
            var bulkCopy = datasetSlice.Stride == 1 && memorySlice.Stride == 1;

            foreach (var datasetBaseOffset in datasetWalker)
            {
                var chunkRemaining = datasetSlice.Count;
                var chunkOffset = datasetBaseOffset;

                while (chunkRemaining > 0)
                {
                    var count = Math.Min(chunkRemaining, memoryRemaining);

                    if (bulkCopy)
                    {
                        var source = chunkBufferSpan.Slice((int)chunkOffset * copyInfo.TypeSize, (int)count * copyInfo.TypeSize);
                        var target = memoryBufferSpan.Slice((int)memoryOffset * copyInfo.TypeSize); 

                        source.CopyTo(target);

                        chunkOffset += count;
                        memoryOffset += count;
                    }
                    else
                    {

#warning Optimzable with intrinsics?
                        for (int i = 0; i < (int)count; i++)
                        {
                            for (int j = 0; j < copyInfo.TypeSize; j++)
                            {
                                memoryBufferSpan[(int)memoryOffset * copyInfo.TypeSize + j]
                                    = chunkBufferSpan[(int)chunkOffset * copyInfo.TypeSize + j];
                            }

                            chunkOffset += datasetSlice.Stride;
                            memoryOffset += memorySlice.Stride;
                        }
                    }

                    chunkRemaining -= count;
                    memoryRemaining -= count;

                    if (memoryRemaining == 0)
                    {
                        memoryRemaining = memorySlice.Length;
                        memoryWalker.MoveNext();
                        memoryOffset = memoryWalker.Current;
                    }
                }
            }
        }
        
        public static IEnumerable<ulong> Walk(int rank, Slice[] slices, ulong[] dims)
        {
            // verify data
            for (int dimension = 0; dimension < rank; dimension++)
            {
                var length = dims[dimension];
                var slice = slices[dimension];
                slice.Verify();

                /* This does not work because stop is not always aligned, somtimes it is "optimized stop" which 
                   might be smaller than the stride length which causes an ulong underflow. */
                //if ((slice.Stop - slice.Stride) >= length)
                //    throw new Exception("The slice length exceeds the buffer length.");
            }

            // walk
            var state = new ulong[rank];
            return InternalWalk(dimension: 0, state, rank, slices, dims);
        }

        private static IEnumerable<ulong> InternalWalk(int dimension, ulong[] state, int rank, Slice[] slices, ulong[] dims)
        {
            // This method is called recursively until state array
            // is filled. Then the actual data are copied.
            var slice = slices[dimension];
            state[dimension] = slice.Start;

            // track progress for all except the last dimension
            if (dimension < rank - 1)
            {
                for (ulong i = 0; i < slice.Count; i++)
                {
                    foreach (var index in HyperslabUtils.InternalWalk(dimension + 1, state, rank, slices, dims))
                    {
                        yield return index;
                    }

                    state[dimension] += slice.Stride;
                }
            }
            // return current offset
            else
            {
                yield return state.ToLinearIndex(dims);
            }
        }

        public static ulong ToLinearIndex(this ulong[] indices, ulong[] dimensions)
        {
            var index = 0UL;
            var rank = indices.Length;

            if (dimensions.Length != rank)
                throw new Exception("Rank of indices and dimensions must be equal.");

            for (int i = 0; i < rank; i++)
            {
                index = index * dimensions[i] + indices[i];
            }

            return index;
        }
    }
}

