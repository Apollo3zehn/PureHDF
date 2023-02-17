using System.Buffers;

namespace PureHDF
{
    internal record CopyInfo<T>(
        ulong[] SourceDims,
        ulong[] SourceChunkDims,
        ulong[] TargetDims,
        ulong[] TargetChunkDims,
        Selection SourceSelection,
        Selection TargetSelection,
        Func<ulong[], Task<Memory<byte>>>? GetSourceBufferAsync,
        Func<ulong[], Stream>? GetSourceStream,
        Func<ulong[], Memory<T>> GetTargetBuffer,
        Action<Memory<byte>, Memory<T>> Converter,
        int TypeSize
    );

    internal readonly struct RelativeStep
    {
        public ulong[] Chunk { get; init; }

        public ulong Offset { get; init; }

        public ulong Length { get; init; }
    }

    internal static class SelectionUtils
    {
        public static Task CopyAsync<TResult, TReader>(
            TReader reader,
            int sourceRank,
            int targetRank,
            CopyInfo<TResult> copyInfo) where TReader : IReader
        {
            /* validate selections */
            if (copyInfo.SourceSelection.TotalElementCount != copyInfo.TargetSelection.TotalElementCount)
                throw new ArgumentException("The lengths of the source selection and target selection are not equal.");

            /* validate rank of dims */
            if (copyInfo.SourceDims.Length != sourceRank ||
                copyInfo.SourceChunkDims.Length != sourceRank ||
                copyInfo.TargetDims.Length != targetRank ||
                copyInfo.TargetChunkDims.Length != targetRank)
                throw new RankException($"The length of each array parameter must match the rank parameter.");

            /* walkers */
            var sourceWalker = Walk(sourceRank, copyInfo.SourceDims, copyInfo.SourceChunkDims, copyInfo.SourceSelection)
                .GetEnumerator();

            var targetWalker = Walk(targetRank, copyInfo.TargetDims, copyInfo.TargetChunkDims, copyInfo.TargetSelection)
                .GetEnumerator();

            /* select method */
            if (copyInfo.GetSourceBufferAsync is not null)
                return CopyMemoryAsync(sourceWalker, targetWalker, copyInfo);

            else if (copyInfo.GetSourceStream is not null)
                return CopyStreamAsync(reader, sourceWalker, targetWalker, copyInfo);

            else
                throw new Exception($"Either GetSourceBuffer or GetSourceStream must be non-null.");
        }

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

        private async static Task CopyMemoryAsync<TResult>(
            IEnumerator<RelativeStep> sourceWalker,
            IEnumerator<RelativeStep> targetWalker,
            CopyInfo<TResult> copyInfo)
        {
            /* initialize source walker */
            var sourceBuffer = default(Memory<byte>);
            var lastSourceChunk = default(ulong[]);

            /* initialize target walker */
            var targetBuffer = default(Memory<TResult>);
            var lastTargetChunk = default(ulong[]);
            var currentTarget = default(Memory<TResult>);

            /* walk until end */
            while (sourceWalker.MoveNext())
            {
                /* load next source buffer */
                var sourceStep = sourceWalker.Current;

                if (sourceBuffer.Length == 0 /* if buffer not assigned yet */ ||
                    !sourceStep.Chunk.SequenceEqual(lastSourceChunk!) /* or the chunk has changed */)
                {
                    sourceBuffer = await copyInfo.GetSourceBufferAsync!(sourceStep.Chunk).ConfigureAwait(false);
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
                            throw new FormatException("The target walker stopped early.");

                        if (targetBuffer.Length == 0 /* if buffer not assigned yet */ ||
                            !targetStep.Chunk.SequenceEqual(lastTargetChunk!) /* or the chunk has changed */)
                        {
                            targetBuffer = copyInfo.GetTargetBuffer(targetStep.Chunk);
                            lastTargetChunk = targetStep.Chunk;
                        }

                        currentTarget = targetBuffer.Slice(
                            (int)targetStep.Offset,
                            (int)targetStep.Length);
                    }

                    /* copy */
                    var length = Math.Min(currentSource.Length / copyInfo.TypeSize, currentTarget.Length);
                    var byteLength = length * copyInfo.TypeSize;

                    copyInfo.Converter(currentSource[..byteLength], currentTarget[..length]);

                    currentSource = currentSource[byteLength..];
                    currentTarget = currentTarget[length..];
                }
            }
        }

        private async static Task CopyStreamAsync<TResult, TReader>(
            TReader reader,
            IEnumerator<RelativeStep> sourceWalker,
            IEnumerator<RelativeStep> targetWalker,
            CopyInfo<TResult> copyInfo) where TReader : IReader
        {
            /* initialize source walker */
            var sourceStream = default(Stream)!;
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
                    sourceStream = copyInfo.GetSourceStream!(sourceStep.Chunk);
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
                            targetBuffer = copyInfo.GetTargetBuffer(targetStep.Chunk);
                            lastTargetChunk = targetStep.Chunk;
                        }

                        currentTarget = targetBuffer.Slice(
                            (int)targetStep.Offset,
                            (int)targetStep.Length);
                    }

                    /* copy */
                    var length = Math.Min(currentLength, currentTarget.Length);

                    if (sourceStream is VirtualDatasetStream<TResult> virtualDatasetStream)
                    {
                        virtualDatasetStream.Seek(currentOffset, SeekOrigin.Begin);

                        await virtualDatasetStream
                            .ReadVirtualAsync(currentTarget[..length])
                            .ConfigureAwait(false);
                    }

                    else
                    {
                        var byteLength = length * copyInfo.TypeSize;

                        // TODO: do not copy if not necessary
                        using var rentedOwner = MemoryPool<byte>.Shared.Rent(byteLength);
                        var rentedMemory = rentedOwner.Memory[..byteLength];

                        await reader.ReadAsync(
                            sourceStream,
                            rentedMemory,
                            currentOffset * copyInfo.TypeSize).ConfigureAwait(false);

                        copyInfo.Converter(rentedMemory, currentTarget[..length]);
                    }

                    currentOffset += length;
                    currentLength -= length;
                    currentTarget = currentTarget[length..];
                }
            }
        }
    }
}

