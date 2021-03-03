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
    internal record CopyInfo
        (int Rank, 
        ulong[] SourceDims,
        ulong[] SourceChunkDims,
        ulong[] TargetDims,
        ulong[] TargetChunkDims,
        HyperslabSelection SourceSelection,
        HyperslabSelection TargetSelection,
        Memory<byte>[] SourceBuffers,
        Memory<byte>[] TargetBuffers,
        int TypeSize);

    internal struct Step
    {
        public ulong Chunk { get; init; }
        public ulong Offset { get; init; }
        public ulong Length { get; init; }
    }

    internal static class HyperslabUtils
    {
        public static void Copy(CopyInfo copyInfo)
        {
            // validate rank
#warning check what happens if e.g. rank = 0

            if (copyInfo.SourceDims.Length != copyInfo.Rank ||
                copyInfo.SourceChunkDims.Length != copyInfo.Rank ||
                copyInfo.TargetDims.Length != copyInfo.Rank ||
                copyInfo.TargetChunkDims.Length != copyInfo.Rank ||
                copyInfo.SourceSelection.Rank != copyInfo.Rank ||
                copyInfo.TargetSelection.Rank != copyInfo.Rank)
                throw new Exception($"The dimensionality of all arrays must be of rank '{copyInfo.Rank}'");

            // validate selections
            if (copyInfo.SourceSelection.GetTotalCount() != copyInfo.TargetSelection.GetTotalCount())
                throw new Exception("The length of the source selection and target selection are not equal.");

            for (int dimension = 0; dimension < copyInfo.Rank; dimension++)
            {
                if (copyInfo.SourceSelection.GetStop(dimension) > copyInfo.SourceDims[dimension])
                    throw new Exception("The source selection size exceeds the limits of the source buffer.");

                if (copyInfo.TargetSelection.GetStop(dimension) > copyInfo.TargetDims[dimension])
                    throw new Exception("The target selection size exceeds the limits of the target buffer.");
            }

            // memory walker
            var sourceWalker = HyperslabUtils
                .Walk(copyInfo.Rank, copyInfo.SourceDims, copyInfo.SourceChunkDims, copyInfo.SourceSelection)
                .GetEnumerator();

            var targetWalker = HyperslabUtils
               .Walk(copyInfo.Rank, copyInfo.TargetDims, copyInfo.TargetChunkDims, copyInfo.TargetSelection)
               .GetEnumerator();

            if (sourceWalker.MoveNext() && targetWalker.MoveNext())
            {

            }

        
        }

        public static IEnumerable<Step> Walk(int rank, ulong[] dims, ulong[] chunkDims, HyperslabSelection selection)
        {
            /* prepare some useful arrays */
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
            var lastDimStop = stops[rank - 1];
            var lastDimBlock = blocks[rank - 1];
            var lastDimGap = gaps[rank - 1];
            var lastChunkDim = chunkDims[rank - 1];
            var supportsBulkCopy = lastDimGap == 0;

            /* loop until all data have been processed */
            while (true)
            {
                /* compute number of consecutive points in current slice */
                ulong totalLength;

                if (supportsBulkCopy)
                    totalLength = lastDimStop - offsets[rank - 1];

                else
                    totalLength = lastDimBlock;

                /* with the full length of consecutive points known, here comes the chunk logic: */
                {
                    var remaining = totalLength;

                    while (remaining > 0)
                    {
                        var offsetsInChunkUnits = new ulong[rank];
                        var chunkOffsets = new ulong[rank];

#warning Optimizable? to not always recalculate chunk data?
                        for (int dimension = 0; dimension < rank; dimension++)
                        {
                            offsetsInChunkUnits[dimension] = offsets[dimension] / chunkDims[dimension];
                            chunkOffsets[dimension] = offsets[dimension] % chunkDims[dimension];
                        }

                        var chunk = offsetsInChunkUnits.ToLinearIndex(datasetDimsInChunkUnits);
                        var offset = chunkOffsets.ToLinearIndex(chunkDims);
                        var currentLength = Math.Min(lastChunkDim - chunkOffsets[rank - 1], remaining);

                        yield return new Step()
                        {
                            Chunk = chunk,
                            Offset = offset,
                            Length = currentLength
                        };

                        remaining -= currentLength;
                        offsets[rank - 1] += currentLength;
                    }
                }

                /* add gap */
                offsets[rank - 1] += lastDimGap;

                /* iterate backwards through all dimensions */
                for (int dimension = rank - 1; dimension >= 0; dimension--)
                {
                    if (dimension != rank - 1)
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

