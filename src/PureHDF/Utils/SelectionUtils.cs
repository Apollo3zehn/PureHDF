using System.Buffers;

namespace PureHDF;

internal record DecodeInfo<T>(
    ulong[] SourceDims,
    ulong[] SourceChunkDims,
    ulong[] TargetDims,
    ulong[] TargetChunkDims,
    Selection SourceSelection,
    Selection TargetSelection,
    Func<ulong[], Task<IH5ReadStream>> GetSourceStreamAsync,
    Func<ulong[], Memory<T>> GetTargetBuffer,
    Action<Memory<byte>, Memory<T>> Decoder,
    int SourceTypeSize,
    int TargetTypeFactor
);

internal record EncodeInfo<T>(
    ulong[] SourceDims,
    ulong[] SourceChunkDims,
    ulong[] TargetDims,
    ulong[] TargetChunkDims,
    Selection SourceSelection,
    Selection TargetSelection,
    Func<ulong[], Memory<T>> GetSourceBuffer,
    Func<ulong[], Task<IH5WriteStream>> GetTargetStreamAsync,
    EncodeDelegate<T> Encoder,
    int TargetTypeSize,
    int SourceTypeFactor
);

internal readonly struct RelativeStep
{
    public ulong[] Chunk { get; init; }

    public ulong Offset { get; init; }

    public ulong Length { get; init; }
}

internal static class SelectionUtils
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

    public static Task EncodeAsync<TSource, TReader>(
        TReader reader,
        int sourceRank,
        int targetRank,
        EncodeInfo<TSource> encodeInfo) where TReader : IReader
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
        return EncodeStreamAsync(reader, sourceWalker, targetWalker, encodeInfo);
    }

    private async static Task EncodeStreamAsync<TResult, TReader>(
        TReader reader,
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        EncodeInfo<TResult> encodeInfo) where TReader : IReader
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
                targetStream = await encodeInfo.GetTargetStreamAsync(targetStep.Chunk).ConfigureAwait(false);
                lastTargetChunk = targetStep.Chunk;
            }

            var currentOffset = (int)targetStep.Offset;
            var currentLength = (int)targetStep.Length;

            while (currentSource.Length > 0)
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
                        (int)sourceStep.Offset * encodeInfo.SourceTypeFactor,
                        (int)sourceStep.Length * encodeInfo.SourceTypeFactor);
                }

                /* copy */
                var length = Math.Min(currentLength, currentSource.Length / encodeInfo.SourceTypeFactor);
                var sourceLength = length * encodeInfo.SourceTypeFactor;

                // optimization; chunked / compact dataset (SystemMemoryStream)
                if (targetStream is SystemMemoryStream systemMemoryStream)
                {
                    systemMemoryStream.Seek(currentOffset * encodeInfo.TargetTypeSize, SeekOrigin.Begin);

                    var currentTarget = systemMemoryStream.SlicedMemory;
                    var targetLength = length * encodeInfo.TargetTypeSize;

                    // TODO, currently 'currentTarget' is unused
                    encodeInfo.Encoder.Invoke(
                        source: currentSource[..sourceLength],
                        target: systemMemoryStream);
                }

                // default; contiguous dataset (OffsetStream)
                // else
                // {
                    // var sourceLength = length * decodeInfo.TargetTypeSize;

                    // // TODO: do not copy if not necessary
                    // using var rentedOwner = MemoryPool<byte>.Shared.Rent(sourceLength);
                    // var rentedMemory = rentedOwner.Memory;

                    // await writer.WriteDatasetAsync(
                    //     sourceBuffer,
                    //     rentedMemory[..sourceLength],
                    //     currentOffset * decodeInfo.TargetTypeSize).ConfigureAwait(false);

                    // decodeInfo.Converter(
                    //     rentedMemory[..sourceLength],
                    //     currentTarget[..targetLength]);
                // }

                currentOffset += length;
                currentLength -= length;
                currentSource = currentSource[sourceLength..];
            }
        }
    }

    public static Task DecodeAsync<TResult, TReader>(
        TReader reader,
        int sourceRank,
        int targetRank,
        DecodeInfo<TResult> decodeInfo) where TReader : IReader
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
        return DecodeStreamAsync(reader, sourceWalker, targetWalker, decodeInfo);
    }

    private async static Task DecodeStreamAsync<TResult, TReader>(
        TReader reader,
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        DecodeInfo<TResult> decodeInfo) where TReader : IReader
    {
        /* initialize source walker */
        var sourceStream = default(IH5ReadStream);
        var lastSourceChunk = default(ulong[]);

        /* initialize target walker */
        var targetBuffer = default(Memory<TResult>);
        var lastTargetChunk = default(ulong[]);
        var currentTarget = default(Memory<TResult>);

        /* walk until end */
        while (sourceWalker.MoveNext())
        {
            /* load next source stream */
            var sourceStep = sourceWalker.Current;

            if (sourceStream is null /* if stream not assigned yet */ ||
                !sourceStep.Chunk.SequenceEqual(lastSourceChunk!) /* or the chunk has changed */)
            {
                sourceStream = await decodeInfo.GetSourceStreamAsync(sourceStep.Chunk).ConfigureAwait(false);
                lastSourceChunk = sourceStep.Chunk;
            }

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

                    if (targetBuffer.Length == 0 /* if buffer not assigned yet */ ||
                        !targetStep.Chunk.SequenceEqual(lastTargetChunk!) /* or the chunk has changed */)
                    {
                        targetBuffer = decodeInfo.GetTargetBuffer(targetStep.Chunk);
                        lastTargetChunk = targetStep.Chunk;
                    }

                    currentTarget = targetBuffer.Slice(
                        (int)targetStep.Offset * decodeInfo.TargetTypeFactor,
                        (int)targetStep.Length * decodeInfo.TargetTypeFactor);
                }

                /* copy */
                var length = Math.Min(currentLength, currentTarget.Length / decodeInfo.TargetTypeFactor);
                var targetLength = length * decodeInfo.TargetTypeFactor;

                // specialization; virtual dataset (VirtualDatasetStream)
                if (sourceStream is VirtualDatasetStream<TResult> virtualDatasetStream)
                {
                    virtualDatasetStream.Seek(currentOffset, SeekOrigin.Begin);

                    await virtualDatasetStream
                        .ReadVirtualAsync(currentTarget[..targetLength])
                        .ConfigureAwait(false);
                }

                // optimization; chunked / compact dataset (SystemMemoryStream)
                else if (sourceStream is SystemMemoryStream systemMemoryStream)
                {
                    systemMemoryStream.Seek(currentOffset * decodeInfo.SourceTypeSize, SeekOrigin.Begin);

                    var currentSource = systemMemoryStream.SlicedMemory;
                    var sourceLength = length * decodeInfo.SourceTypeSize;

                    decodeInfo.Decoder(
                        currentSource[..sourceLength],
                        currentTarget[..targetLength]);
                }

                // default; contiguous dataset (OffsetStream, ExternalFileListStream (wrapping a SlotStream), UnsafeFillValueStream)
                else
                {
                    var sourceLength = length * decodeInfo.SourceTypeSize;

                    // TODO: do not copy if not necessary
                    using var rentedOwner = MemoryPool<byte>.Shared.Rent(sourceLength);
                    var rentedMemory = rentedOwner.Memory;

                    await reader.ReadDatasetAsync(
                        sourceStream,
                        rentedMemory[..sourceLength],
                        currentOffset * decodeInfo.SourceTypeSize).ConfigureAwait(false);

                    decodeInfo.Decoder(
                        rentedMemory[..sourceLength],
                        currentTarget[..targetLength]);
                }

                currentOffset += length;
                currentLength -= length;
                currentTarget = currentTarget[targetLength..];
            }
        }
    }
}