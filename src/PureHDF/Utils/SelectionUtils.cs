using System.Buffers;

namespace PureHDF;

internal record ReadInfo<T>(
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

internal record WriteInfo<T>(
    ulong[] SourceDims,
    ulong[] SourceChunkDims,
    ulong[] TargetDims,
    ulong[] TargetChunkDims,
    Selection SourceSelection,
    Selection TargetSelection,
    Func<ulong[], Memory<T>> GetSourceBuffer,
    Func<ulong[], Task<Stream>> GetTargetStreamAsync,
    Action<Memory<T>, Memory<byte>> Encoder,
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

    public static Task WriteAsync<TSource, TReader>(
        TReader reader,
        int sourceRank,
        int targetRank,
        WriteInfo<TSource> writeInfo) where TReader : IReader
    {
        /* validate selections */
        if (writeInfo.SourceSelection.TotalElementCount != writeInfo.TargetSelection.TotalElementCount)
            throw new ArgumentException("The lengths of the source selection and target selection are not equal.");

        /* validate rank of dims */
        if (writeInfo.SourceDims.Length != sourceRank ||
            writeInfo.SourceChunkDims.Length != sourceRank ||
            writeInfo.TargetDims.Length != targetRank ||
            writeInfo.TargetChunkDims.Length != targetRank)
            throw new RankException($"The length of each array parameter must match the rank parameter.");

        /* walkers */
        var sourceWalker = Walk(sourceRank, writeInfo.SourceDims, writeInfo.SourceChunkDims, writeInfo.SourceSelection)
            .GetEnumerator();

        var targetWalker = Walk(targetRank, writeInfo.TargetDims, writeInfo.TargetChunkDims, writeInfo.TargetSelection)
            .GetEnumerator();

        /* select method */
        return WriteStreamAsync(reader, sourceWalker, targetWalker, writeInfo);
    }

    private async static Task WriteStreamAsync<TResult, TReader>(
        TReader reader,
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        WriteInfo<TResult> writeInfo) where TReader : IReader
    {
        /* initialize source walker */
        var sourceBuffer = default(Memory<TResult>);
        var lastSourceChunk = default(ulong[]);
        var currentSource = default(Memory<TResult>);

        /* initialize target walker */
        var targetStream = default(Stream);
        var lastTargetChunk = default(ulong[]);

        /* walk until end */
        while (sourceWalker.MoveNext())
        {
            /* load next source buffer */
            var sourceStep = sourceWalker.Current;

            if (currentSource.Length == 0 /* if buffer not assigned yet */ ||
                !sourceStep.Chunk.SequenceEqual(lastSourceChunk!) /* or the chunk has changed */)
            {
                sourceBuffer = writeInfo.GetSourceBuffer(sourceStep.Chunk);
                lastSourceChunk = sourceStep.Chunk;
            }

            currentSource = sourceBuffer.Slice(
                (int)sourceStep.Offset,
                (int)sourceStep.Length);

            while (currentSource.Length > 0)
            {
                /* load next target stream */
                if (targetStream is null)
                {
                    var success = targetWalker.MoveNext();
                    var targetStep = targetWalker.Current;

                    if (!success || targetStep.Length == 0)
                        throw new FormatException("The target walker stopped early.");

                    if (targetStream is null /* if stream not assigned yet */ ||
                        !targetStep.Chunk.SequenceEqual(lastTargetChunk!) /* or the chunk has changed */)
                    {
                        targetStream = await writeInfo
                            .GetTargetStreamAsync(targetStep.Chunk)
                            .ConfigureAwait(false);

                        lastTargetChunk = targetStep.Chunk;
                    }
                }

                var currentOffset = (int)targetStep.Offset * writeInfo.SourceTypeFactor;
                var currentLength = (int)targetStep.Length * writeInfo.SourceTypeFactor;

                /* copy */
                var length = Math.Min(currentLength, currentSource.Length / writeInfo.SourceTypeFactor);
                var sourceLength = length * writeInfo.SourceTypeFactor;

                // optimization; chunked / compact dataset (SystemMemoryStream)
                if (targetStream is SystemMemoryStream systemMemoryStream)
                {
                    systemMemoryStream.Seek(currentOffset * writeInfo.TargetTypeSize, SeekOrigin.Begin);

                    var currentTarget = systemMemoryStream.SlicedMemory;
                    var targetLength = length * writeInfo.TargetTypeSize;

                    writeInfo.Encoder(
                        currentSource[..sourceLength],
                        currentTarget[..targetLength]);
                }

                // default; contiguous dataset (OffsetStream)
                // else
                // {
                    // var sourceLength = length * writeInfo.TargetTypeSize;

                    // // TODO: do not copy if not necessary
                    // using var rentedOwner = MemoryPool<byte>.Shared.Rent(sourceLength);
                    // var rentedMemory = rentedOwner.Memory;

                    // await reader.ReadDatasetAsync(
                    //     sourceBuffer,
                    //     rentedMemory[..sourceLength],
                    //     currentOffset * writeInfo.TargetTypeSize).ConfigureAwait(false);

                    // writeInfo.Converter(
                    //     rentedMemory[..sourceLength],
                    //     currentTarget[..targetLength]);
                // }

                currentOffset += length;
                currentLength -= length;
                currentSource = currentSource[sourceLength..];
            }
        }
    }


    public static Task ReadAsync<TResult, TReader>(
        TReader reader,
        int sourceRank,
        int targetRank,
        ReadInfo<TResult> readInfo) where TReader : IReader
    {
        /* validate selections */
        if (readInfo.SourceSelection.TotalElementCount != readInfo.TargetSelection.TotalElementCount)
            throw new ArgumentException("The lengths of the source selection and target selection are not equal.");

        /* validate rank of dims */
        if (readInfo.SourceDims.Length != sourceRank ||
            readInfo.SourceChunkDims.Length != sourceRank ||
            readInfo.TargetDims.Length != targetRank ||
            readInfo.TargetChunkDims.Length != targetRank)
            throw new RankException($"The length of each array parameter must match the rank parameter.");

        /* walkers */
        var sourceWalker = Walk(sourceRank, readInfo.SourceDims, readInfo.SourceChunkDims, readInfo.SourceSelection)
            .GetEnumerator();

        var targetWalker = Walk(targetRank, readInfo.TargetDims, readInfo.TargetChunkDims, readInfo.TargetSelection)
            .GetEnumerator();

        /* select method */
        return ReadStreamAsync(reader, sourceWalker, targetWalker, readInfo);
    }

    private async static Task ReadStreamAsync<TResult, TReader>(
        TReader reader,
        IEnumerator<RelativeStep> sourceWalker,
        IEnumerator<RelativeStep> targetWalker,
        ReadInfo<TResult> readInfo) where TReader : IReader
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
                sourceStream = await readInfo.GetSourceStreamAsync(sourceStep.Chunk).ConfigureAwait(false);
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
                        targetBuffer = readInfo.GetTargetBuffer(targetStep.Chunk);
                        lastTargetChunk = targetStep.Chunk;
                    }

                    currentTarget = targetBuffer.Slice(
                        (int)targetStep.Offset * readInfo.TargetTypeFactor,
                        (int)targetStep.Length * readInfo.TargetTypeFactor);
                }

                /* copy */
                var length = Math.Min(currentLength, currentTarget.Length / readInfo.TargetTypeFactor);
                var targetLength = length * readInfo.TargetTypeFactor;

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
                    systemMemoryStream.Seek(currentOffset * readInfo.SourceTypeSize, SeekOrigin.Begin);

                    var currentSource = systemMemoryStream.SlicedMemory;
                    var sourceLength = length * readInfo.SourceTypeSize;

                    readInfo.Converter(
                        currentSource[..sourceLength],
                        currentTarget[..targetLength]);
                }

                // default; contiguous dataset (OffsetStream, ExternalFileListStream (wrapping a SlotStream), UnsafeFillValueStream)
                else
                {
                    var sourceLength = length * readInfo.SourceTypeSize;

                    // TODO: do not copy if not necessary
                    using var rentedOwner = MemoryPool<byte>.Shared.Rent(sourceLength);
                    var rentedMemory = rentedOwner.Memory;

                    await reader.ReadDatasetAsync(
                        sourceStream,
                        rentedMemory[..sourceLength],
                        currentOffset * readInfo.SourceTypeSize).ConfigureAwait(false);

                    readInfo.Converter(
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