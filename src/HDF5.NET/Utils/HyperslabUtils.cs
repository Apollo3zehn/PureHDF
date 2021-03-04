using System;
using System.Collections.Generic;
using System.Linq;

#warning Workaround for https://stackoverflow.com/questions/62648189/testing-c-sharp-9-0-in-vs2019-cs0518-isexternalinit-is-not-defined-or-imported
namespace System.Runtime.CompilerServices
{
    public record IsExternalInit;
}

namespace HDF5.NET
{
    internal record CopyInfo(
        ulong[] SourceDims,
        ulong[] SourceChunkDims,
        ulong[] TargetDims,
        ulong[] TargetChunkDims,
        HyperslabSelection SourceSelection,
        HyperslabSelection TargetSelection,
        Func<ulong, Memory<byte>> GetSourceBuffer,
        Func<ulong, Memory<byte>> GetTargetBuffer,
        int TypeSize
    );

    internal struct Step
    {
        public ulong Chunk { get; init; }
        public ulong Offset { get; init; }
        public ulong Length { get; init; }
    }

    internal static class HyperslabUtils
    {
        public static IEnumerable<Step> Walk(int rank, ulong[] dims, ulong[] chunkDims, HyperslabSelection selection)
        {
            /* check if there is anything to do */
            if (selection.GetTotalCount() == 0)
                yield break;

            /* validate rank */
            if (dims.Length != rank || chunkDims.Length != rank)
                throw new RankException($"The length of each array parameter must match the rank parameter.");

            /* prepare some useful arrays */
            var lastDim = rank - 1;
            var offsets = new ulong[rank];
            var stops = new ulong[rank];
            var strides = new ulong[rank];
            var blocks = new ulong[rank];
            var gaps = new ulong[rank];
            var datasetDimsInChunkUnits = new ulong[rank];
            var chunkLength = chunkDims.Aggregate(1UL, (x, y) => x * y);

            for (int dimension = 0; dimension < rank; dimension++)
            {
                offsets[dimension] = selection.Starts[dimension];
                stops[dimension] = selection.GetStop(dimension);
                strides[dimension] = selection.Strides[dimension];
                blocks[dimension] = selection.Blocks[dimension];
                gaps[dimension] = strides[dimension] - blocks[dimension];
                datasetDimsInChunkUnits[dimension] = H5Utils.CeilDiv(dims[dimension], chunkDims[dimension]);
            }

            /* prepare last dimension variables */
            var lastDimStop = stops[lastDim];
            var lastDimBlock = blocks[lastDim];
            var lastDimGap = gaps[lastDim];
            var lastChunkDim = chunkDims[lastDim];
            var supportsBulkCopy = lastDimGap == 0;

            /* loop until all data have been processed */
            while (true)
            {
                /* compute number of consecutive points in current slice */
                ulong totalLength;

                if (supportsBulkCopy)
                    totalLength = lastDimStop - offsets[lastDim];

                else
                    totalLength = lastDimBlock;

                /* with the full length of consecutive points known, we continue with the chunk logic:
                 * (there was an attempt to reduce the number of chunk calculations but that did not
                 * result in significant performance improvements, so it has been reverted)
                 */
                {
                    var remaining = totalLength;

                    while (remaining > 0)
                    {
                        var offsetsInChunkUnits = new ulong[rank];
                        var chunkOffsets = new ulong[rank];

                        for (int dimension = 0; dimension < rank; dimension++)
                        {
                            offsetsInChunkUnits[dimension] = offsets[dimension] / chunkDims[dimension];
                            chunkOffsets[dimension] = offsets[dimension] % chunkDims[dimension];
                        }

                        var chunk = offsetsInChunkUnits.ToLinearIndex(datasetDimsInChunkUnits);
                        var offset = chunkOffsets.ToLinearIndex(chunkDims);
                        var currentLength = Math.Min(lastChunkDim - chunkOffsets[lastDim], remaining);

                        yield return new Step()
                        {
                            Chunk = chunk,
                            Offset = offset,
                            Length = currentLength
                        };

                        remaining -= currentLength;
                        offsets[lastDim] += currentLength;
                    }
                }

                /* add gap */
                offsets[lastDim] += lastDimGap;

                /* iterate backwards through all dimensions */
                for (int dimension = lastDim; dimension >= 0; dimension--)
                {
                    if (dimension != lastDim)
                    {
                        /* go one step forward */
                        offsets[dimension] += 1;

                        /* if we have reached a gap, skip that gap */
                        var consumedStride = (offsets[dimension] - selection.Starts[dimension]) % strides[dimension];

                        if (consumedStride == blocks[dimension])
                            offsets[dimension] += gaps[dimension];
                    }

                    /* if the current slice is fully processed */
                    if (offsets[dimension] >= stops[dimension])
                    {
                        /* if there is more to process, reset the offset and 
                         * repeat the loop for the next higher dimension */
                        if (dimension > 0)
                            offsets[dimension] = selection.Starts[dimension];

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

        public static void Copy(int rank, CopyInfo copyInfo)
        {
            /* validate rank */
            if (copyInfo.SourceSelection.Rank != rank ||
                copyInfo.TargetSelection.Rank != rank)
                throw new RankException($"The length of each array parameter must match the rank parameter.");

            /* validate selections */
            if (copyInfo.SourceSelection.GetTotalCount() != copyInfo.TargetSelection.GetTotalCount())
                throw new ArgumentException("The length of the source selection and target selection are not equal.");

            for (int dimension = 0; dimension < rank; dimension++)
            {
                if (copyInfo.SourceSelection.GetStop(dimension) > copyInfo.SourceDims[dimension])
                    throw new ArgumentException("The source selection size exceeds the limits of the source buffer.");

                if (copyInfo.TargetSelection.GetStop(dimension) > copyInfo.TargetDims[dimension])
                    throw new ArgumentException("The target selection size exceeds the limits of the target buffer.");
            }

            /* memory walker */
            var sourceWalker = HyperslabUtils
                .Walk(rank, copyInfo.SourceDims, copyInfo.SourceChunkDims, copyInfo.SourceSelection)
                .GetEnumerator();

            var targetWalker = HyperslabUtils
               .Walk(rank, copyInfo.TargetDims, copyInfo.TargetChunkDims, copyInfo.TargetSelection)
               .GetEnumerator();

            /* initialize source walker */
            var sourceBuffer = default(Memory<byte>);
            var lastSourceChunk = 0UL;

            /* initialize target walker */
            var targetBuffer = default(Memory<byte>);
            var lastTargetChunk = 0UL;
            var currentTarget = default(Memory<byte>);

            /* walk until end */
            while (sourceWalker.MoveNext())
            {
                /* load next source buffer */
                var sourceStep = sourceWalker.Current;

                if (sourceBuffer.Length == 0 /* if buffer not assigned yet */ || 
                    sourceStep.Chunk != lastSourceChunk /* or the chunk has changed */)
                {
                    sourceBuffer = copyInfo.GetSourceBuffer(sourceStep.Chunk);
                    lastSourceChunk = sourceStep.Chunk;
                }

                var currentSource = sourceBuffer.Slice(
                    (int)sourceStep.Offset * copyInfo.TypeSize, 
                    (int)sourceStep.Length * copyInfo.TypeSize);

                while (currentSource.Length > 0)
                {
                    /* load next target buffer */
                    if (currentTarget.Length == 0)
                    {
                        var success = targetWalker.MoveNext();
                        var targetStep = targetWalker.Current;

                        if (!success || targetStep.Length == 0)
                            throw new UriFormatException("The target walker stopped early.");

                        if (targetBuffer.Length == 0 /* if buffer not assigned yet */ || 
                            targetStep.Chunk != lastTargetChunk /* or the chunk has changed */)
                        {
                            targetBuffer = copyInfo.GetTargetBuffer(targetStep.Chunk);
                            lastTargetChunk = targetStep.Chunk;
                        }
                        
                        currentTarget = targetBuffer.Slice(
                            (int)targetStep.Offset * copyInfo.TypeSize, 
                            (int)targetStep.Length * copyInfo.TypeSize);
                    }

                    /* copy */
                    var length = Math.Min(currentSource.Length, currentTarget.Length);

                    currentSource
                        .Slice(0, length)
                        .CopyTo(currentTarget);

                    currentSource = currentSource.Slice(length);
                    currentTarget = currentTarget.Slice(length);
                }
            }
        }

        public static ulong ToLinearIndex(this ulong[] indices, ulong[] dimensions)
        {
            var index = 0UL;
            var rank = indices.Length;

            if (dimensions.Length != rank)
                throw new Exception("Rank of index and dimension arrays must be equal.");

            for (int i = 0; i < rank; i++)
            {
                index = index * dimensions[i] + indices[i];
            }

            return index;
        }
    }
}

