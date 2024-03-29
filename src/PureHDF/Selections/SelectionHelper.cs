namespace PureHDF.Selections;

internal record DecodeInfo<T>(
    ulong[] SourceDims,
    ulong[] SourceChunkDims,
    ulong[] TargetDims,
    ulong[] TargetChunkDims,
    Selection SourceSelection,
    Selection TargetSelection,
    Func<ulong[], IH5ReadStream> GetSourceStream,
    DecodeDelegate<T> Decoder,
    int SourceTypeSize
);

internal record EncodeInfo<T>(
    ulong[] SourceDims,
    ulong[] SourceChunkDims,
    ulong[] TargetDims,
    ulong[] TargetChunkDims,
    Selection SourceSelection,
    Selection TargetSelection,
    Func<ulong[], Memory<T>> GetSourceBuffer,
    Func<ulong[], IH5WriteStream> GetTargetStream,
    EncodeDelegate<T> Encoder,
    int TargetTypeSize
);

internal readonly record struct RelativeStep(
    ulong[] Chunk,
    ulong Offset,
    ulong Length
);

internal static class SelectionHelper
{
    public static IEnumerable<RelativeStep> Walk(int rank, ulong[] dims, ulong[] chunkDims, Selection selection)
    {
        /* check if there is anything to do */
        if (selection.TotalElementCount == 0)
            yield break;

        /* validate rank */
        if (dims.Length != rank || chunkDims.Length != rank)
            throw new RankException($"The length of each array parameter must match the rank parameter.");

        /* prepare some useful arrays */
        var lastDim = rank - 1;
        var chunkLength = chunkDims.Aggregate(1UL, (x, y) => x * y);

        /* prepare last dimension variables */
        var lastChunkDim = chunkDims[lastDim];

        foreach (var step in selection.Walk(limits: dims))
        {
            /* validate rank */
            if (step.Coordinates.Length != rank)
                throw new RankException($"The length of the step coordinates array must match the rank parameter.");

            var remaining = step.ElementCount;

            /* process slice */
            while (remaining > 0)
            {
                // TODO: Performance issue.
                var scaledOffsets = new ulong[rank];
                Span<ulong> chunkOffsets = stackalloc ulong[rank];

                for (int dimension = 0; dimension < rank; dimension++)
                {
                    scaledOffsets[dimension] = step.Coordinates[dimension] / chunkDims[dimension];
                    chunkOffsets[dimension] = step.Coordinates[dimension] % chunkDims[dimension];
                }

                var offset = chunkOffsets.ToLinearIndex(chunkDims);
                var currentLength = Math.Min(lastChunkDim - chunkOffsets[lastDim], remaining);

                yield return new RelativeStep()
                {
                    Chunk = scaledOffsets,
                    Offset = offset,
                    Length = currentLength
                };

                remaining -= currentLength;
                step.Coordinates[lastDim] += currentLength;
            }
        }
    }

    public static void Encode<TSource>(
        int sourceRank,
        int targetRank,
        EncodeInfo<TSource> encodeInfo)
    {
        /* validate selections */
        if (encodeInfo.SourceSelection.TotalElementCount != encodeInfo.TargetSelection.TotalElementCount)
            throw new ArgumentException("The lengths of the source selection and target selection are not equal.");

        /* validate rank of dims */
        if (encodeInfo.SourceDims.Length != sourceRank ||
            encodeInfo.SourceChunkDims.Length != sourceRank ||
            encodeInfo.TargetDims.Length != targetRank ||
            encodeInfo.TargetChunkDims.Length != targetRank)
            throw new RankException($"The length of each array parameter must match the rank parameter.");

        /* walkers */
        var sourceWalker = Walk(sourceRank, encodeInfo.SourceDims, encodeInfo.SourceChunkDims, encodeInfo.SourceSelection)
            .GetEnumerator();

        var targetWalker = Walk(targetRank, encodeInfo.TargetDims, encodeInfo.TargetChunkDims, encodeInfo.TargetSelection)
            .GetEnumerator();

        /* select method */
        EncodeStream(sourceWalker, targetWalker, encodeInfo);
    }

    private static void EncodeStream<TResult>(
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        EncodeInfo<TResult> encodeInfo)
    {
        /* initialize source walker */
        var sourceBuffer = default(Memory<TResult>);
        var lastSourceChunk = default(ulong[]);
        var currentSource = default(Memory<TResult>);

        /* initialize target walker */
        var targetStream = default(IH5WriteStream);
        var lastTargetChunk = default(ulong[]);

        /* walk until end */
        while (targetWalker.MoveNext())
        {
            /* load next target stream */
            var targetStep = targetWalker.Current;

            if (targetStream is null /* if stream not assigned yet */ ||
                !targetStep.Chunk.SequenceEqual(lastTargetChunk!) /* or the chunk has changed */)
            {
                targetStream = encodeInfo.GetTargetStream(targetStep.Chunk);
                lastTargetChunk = targetStep.Chunk;
            }

            var currentOffset = (int)targetStep.Offset;
            var currentLength = (int)targetStep.Length;

            while (currentLength > 0)
            {
                /* load next source buffer */
                if (currentSource.Length == 0)
                {
                    var success = sourceWalker.MoveNext();
                    var sourceStep = sourceWalker.Current;

                    if (!success || sourceStep.Length == 0)
                        throw new FormatException("The source walker stopped early.");

                    if (sourceBuffer.Length == 0 /* if buffer not assigned yet */ ||
                        !sourceStep.Chunk.SequenceEqual(lastSourceChunk!) /* or the chunk has changed */)
                    {
                        sourceBuffer = encodeInfo.GetSourceBuffer(sourceStep.Chunk);
                        lastSourceChunk = sourceStep.Chunk;
                    }

                    currentSource = sourceBuffer.Slice(
                        (int)sourceStep.Offset,
                        (int)sourceStep.Length);
                }

                /* copy */
                var length = Math.Min(currentLength, currentSource.Length);
                var sourceLength = length;

                targetStream.Seek(currentOffset * encodeInfo.TargetTypeSize, SeekOrigin.Begin);

                encodeInfo.Encoder(
                    currentSource[..sourceLength],
                    targetStream);

                currentOffset += length;
                currentLength -= length;
                currentSource = currentSource[sourceLength..];
            }
        }
    }

    public static void Decode<TResult>(
        int sourceRank,
        int targetRank,
        DecodeInfo<TResult> decodeInfo,
        Span<TResult> targetBuffer)
    {
        /* validate selections */
        if (decodeInfo.SourceSelection.TotalElementCount != decodeInfo.TargetSelection.TotalElementCount)
            throw new ArgumentException("The lengths of the source selection and target selection are not equal.");

        /* validate rank of dims */
        if (decodeInfo.SourceDims.Length != sourceRank ||
            decodeInfo.SourceChunkDims.Length != sourceRank ||
            decodeInfo.TargetDims.Length != targetRank ||
            decodeInfo.TargetChunkDims.Length != targetRank)
            throw new RankException($"The length of each array parameter must match the rank parameter.");

        /* walkers */
        var sourceWalker = Walk(sourceRank, decodeInfo.SourceDims, decodeInfo.SourceChunkDims, decodeInfo.SourceSelection)
            .GetEnumerator();

        var targetWalker = Walk(targetRank, decodeInfo.TargetDims, decodeInfo.TargetChunkDims, decodeInfo.TargetSelection)
            .GetEnumerator();

        /* select method */
        DecodeStream(sourceWalker, targetWalker, decodeInfo, targetBuffer);
    }

    private static void DecodeStream<TResult>(
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        DecodeInfo<TResult> decodeInfo,
        Span<TResult> targetBuffer)
    {
        /* initialize source walker */
        var sourceStream = default(IH5ReadStream);
        var lastSourceChunk = default(ulong[]);

        /* initialize target walker */
        var currentTarget = default(Span<TResult>);

        /* walk until end */
        while (sourceWalker.MoveNext())
        {
            /* load next source stream */
            var sourceStep = sourceWalker.Current;

            if (sourceStream is null /* if stream not assigned yet */ ||
                !sourceStep.Chunk.SequenceEqual(lastSourceChunk!) /* or the chunk has changed */)
            {
                sourceStream = decodeInfo.GetSourceStream(sourceStep.Chunk);
                lastSourceChunk = sourceStep.Chunk;
            }

            var virtualDatasetStream = sourceStream as VirtualDatasetStream<TResult>;
            var currentOffset = (int)sourceStep.Offset;
            var currentLength = (int)sourceStep.Length;

            while (currentLength > 0)
            {
                /* load next target buffer */
                if (currentTarget.Length == 0)
                {
                    var success = targetWalker.MoveNext();
                    var targetStep = targetWalker.Current;

                    if (!success || targetStep.Length == 0)
                        throw new FormatException("The target walker stopped early.");

                    currentTarget = targetBuffer.Slice(
                        (int)targetStep.Offset,
                        (int)targetStep.Length);
                }

                /* copy */

                var length = Math.Min(currentLength, currentTarget.Length);
                var targetLength = length;

                if (virtualDatasetStream is null)
                {
                    sourceStream.Seek(currentOffset * decodeInfo.SourceTypeSize, SeekOrigin.Begin);

                    decodeInfo.Decoder(
                        sourceStream,
                        currentTarget[..targetLength]);
                }

                else
                {
                    virtualDatasetStream.Seek(currentOffset, SeekOrigin.Begin);

                    virtualDatasetStream.ReadVirtual(
                        currentTarget[..targetLength]);
                }

                currentOffset += length;
                currentLength -= length;
                currentTarget = currentTarget[targetLength..];
            }
        }
    }
}