using System.Buffers;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal abstract class H5D_Chunk : H5D_Base
{
    #region Types

    protected record struct ChunkInfo(ulong Address, ulong Size, uint FilterMask)
    {
        public static ChunkInfo None { get; } = new ChunkInfo(Superblock.UndefinedAddress, 0, 0);
    }

    #endregion

    #region Fields

    private readonly IReadingChunkCache _readingChunkCache = default!;
    private readonly IWritingChunkCache _writingChunkCache = default!;
    private readonly bool _readingChunkIndexAddressIsUndefined;

    // Writing: how many elements each partly written chunk has received, and which chunks are finished.
    // Created on the first write that needs them, so that the layouts a writer of many chunked datasets
    // holds are not mostly empty collections.
    private Dictionary<ulong, ulong>? _elementsWritten;
    private HashSet<ulong>? _finishedChunks;

    // Chunk buffers are pooled only with the default cache, which is known to drop a chunk once it has
    // handed it to the chunk writer. A caller's cache might keep it, and a returned buffer would then be
    // shared with whoever rents it next.
    private readonly bool _poolChunks;

    #endregion

    #region Constructors

    public H5D_Chunk(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation)
        : base(readContext, writeContext, dataset, datasetAccess)
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        if (readContext is null)
        {
            _writingChunkCache = datasetCreation.ChunkCache ?? ChunkCache.DefaultWritingChunkCacheFactory();
            _poolChunks = _writingChunkCache.GetType() == typeof(SimpleWritingChunkCache);
        }

        else
        {
            _readingChunkCache = datasetAccess.ChunkCache ?? ChunkCache.DefaultReadingChunkCacheFactory();

            var address = Dataset.Layout switch
            {
                DataLayoutMessage12 layout12 => layout12.Address,
                DataLayoutMessage3 layout3 => ((ChunkedStoragePropertyDescription)layout3.Properties).Address,
                _ => throw new Exception($"The layout {Dataset.Layout} is not supported.")
            };

            _readingChunkIndexAddressIsUndefined = readContext.Superblock
                .IsUndefinedAddress(address);
        }
    }

    #endregion

    #region Properties

    // these properties will all be initialized in Initialize()

    public ulong[] RawChunkDims { get; private set; } = default!;

    public ulong[] ChunkDims { get; private set; } = default!;

    public byte ChunkRank { get; private set; }

    public ulong ChunkByteSize { get; private set; }

    public uint ChunkSizeLength { get; private set; }

    public ulong[] Dims { get; private set; } = default!;

    public ulong[] MaxDims { get; private set; } = default!;

    public ulong[] ScaledDims { get; private set; } = default!;

    public ulong[] ScaledMaxDims { get; private set; } = default!;

    public ulong[] DownChunkCounts { get; private set; } = default!;

    public ulong[] DownMaxChunkCounts { get; private set; } = default!;

    public ulong TotalChunkCount { get; private set; }

    public ulong TotalMaxChunkCount { get; private set; }

    /// <summary>
    /// Whether every chunk of a filtered dataset has been compressed and written, so that nothing about
    /// its data can change any more and its index can be written.
    /// </summary>
    /// <remarks>
    /// Only a filtered dataset tracks its chunks: an unfiltered chunk is written straight to its place,
    /// and may be written again.
    /// </remarks>
    public bool IsComplete => Dataset.FilterPipeline is not null &&
        (ulong)(_finishedChunks?.Count ?? 0) == TotalChunkCount;

    #endregion

    #region Methods

    public static H5D_Chunk Create(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation)
    {
        return dataset.Layout switch
        {
            DataLayoutMessage12 layout12 => new H5D_Chunk123_BTree1(readContext, writeContext, dataset, layout12, datasetAccess, datasetCreation),

            DataLayoutMessage4 layout4 => ((ChunkedStoragePropertyDescription4)layout4.Properties).IndexingInformation switch
            {
                // the current, maximum, and chunk dimension sizes are all the same
                SingleChunkIndexingInformation => new H5Dataset_Chunk_Single_Chunk4(readContext, writeContext, dataset, layout4, datasetAccess, datasetCreation),

                // fixed maximum dimension sizes
                // no filter applied to the dataset
                // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                ImplicitIndexingInformation => new H5D_Chunk4_Implicit(readContext, writeContext, dataset, layout4, datasetAccess, datasetCreation),

                // fixed maximum dimension sizes
                FixedArrayIndexingInformation => new H5D_Chunk4_FixedArray(readContext, writeContext, dataset, layout4, datasetAccess, datasetCreation),

                // only one dimension of unlimited extent
                ExtensibleArrayIndexingInformation => new H5D_Chunk4_ExtensibleArray(readContext, writeContext, dataset, layout4, datasetAccess, datasetCreation),

                // more than one dimension of unlimited extent
                BTree2IndexingInformation => new H5D_Chunk4_BTree2(readContext, writeContext, dataset, layout4, datasetAccess, datasetCreation),
                _ => throw new Exception("Unknown chunk indexing type.")
            },

            DataLayoutMessage3 layout3 => new H5D_Chunk123_BTree1(readContext, writeContext, dataset, layout3, datasetAccess, datasetCreation),

            _ => throw new Exception($"Data layout message type '{dataset.Layout.GetType().Name}' is not supported.")
        };
    }

    public override void Initialize()
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        RawChunkDims = GetRawChunkDims();
        ChunkDims = RawChunkDims[..^1].ToArray();
        ChunkRank = (byte)ChunkDims.Length;
        ChunkByteSize = ChunkDims.Aggregate(1UL, (product, dim) => product * dim) * Dataset.Type.Size;
        ChunkSizeLength = MathUtils.ComputeChunkSizeLength(ChunkByteSize);
        Dims = Dataset.Space.Dimensions;
        MaxDims = Dataset.Space.MaxDimensions;
        TotalChunkCount = 1;
        TotalMaxChunkCount = 1;

        ScaledDims = new ulong[ChunkRank];
        ScaledMaxDims = new ulong[ChunkRank];

        for (int i = 0; i < ChunkRank; i++)
        {
            ScaledDims[i] = MathUtils.CeilDiv(Dims[i], ChunkDims[i]);

            if (MaxDims[i] == H5Constants.Unlimited)
                ScaledMaxDims[i] = H5Constants.Unlimited;

            else
                ScaledMaxDims[i] = MathUtils.CeilDiv(MaxDims[i], ChunkDims[i]);

            TotalChunkCount *= ScaledDims[i];

            if (ScaledMaxDims[i] == H5Constants.Unlimited || TotalMaxChunkCount == H5Constants.Unlimited)
                TotalMaxChunkCount = H5Constants.Unlimited;

            else
                TotalMaxChunkCount *= ScaledMaxDims[i];
        }

        /* Get the "down" sizes for each dimension */
        DownChunkCounts = ScaledDims.AccumulateReverse();
        DownMaxChunkCounts = ScaledMaxDims.AccumulateReverse();
    }

    public override ulong[] GetChunkDims()
    {
        return ChunkDims;
    }

    public override async ValueTask<IH5ReadStream> GetReadStream(ulong chunkIndex)
    {
        // The one layout where resolving a stream genuinely reads: on a cache miss this fetches (and
        // possibly decompresses) the chunk. A third-party IReadingChunkCache that does not override
        // GetChunkAsync still works, but blocks - see the default implementation on the interface.
        var buffer = await _readingChunkCache
            .GetChunkAsync(
                chunkIndex,
                chunkReader: () => ReadChunk(chunkIndex))
            .ConfigureAwait(false);

        var stream = new SystemMemoryStream(buffer);

        return stream;
    }

    public override IH5WriteStream GetWriteStream(ulong chunkIndex)
    {
        // A chunk with a fixed place in the file is written there directly, like contiguous data. Only a
        // chunk that has to be complete before it can be written - a filtered one, which is compressed as
        // a whole - is buffered.
        if (TryGetWriteThroughAddress(chunkIndex, out var address))
        {
            WriteContext.Driver.SeekRelativeToBaseAddress((long)address);
            return new PayloadWriteStream(WriteContext.Driver);
        }

        if (_finishedChunks is not null && _finishedChunks.Contains(chunkIndex))
            throw new InvalidOperationException(
                $"Chunk {chunkIndex} has already been written. Each element of a chunked dataset can be written " +
                "once: a chunk is compressed and written as soon as all of its elements have arrived.");

        var buffer = _writingChunkCache
            .GetChunk(
                chunkIndex,
                chunkAllocator: AllocateChunk,
                chunkWriter: WriteAndReleaseChunk);

        return new ChunkWriteStream(buffer);
    }

    /// <summary>
    /// Counts the elements an encode has put into a buffered chunk, and writes the chunk once it is complete.
    /// </summary>
    /// <remarks>
    /// Counted in elements, from the selection walk, rather than in bytes from the stream: an encoder may
    /// seek over bytes it leaves zero, such as padding, so the bytes written need not add up to the chunk.
    /// Assumes each element is written once - writing an element twice before its chunk is complete makes
    /// the chunk look complete early, and the write that follows then fails rather than going unnoticed.
    /// </remarks>
    public void OnElementsWritten(ulong chunkIndex, int elementCount, IH5WriteStream stream)
    {
        if (stream is not ChunkWriteStream chunkStream)
            return;

        var written = (_elementsWritten?.GetValueOrDefault(chunkIndex) ?? 0) + (ulong)elementCount;

        if (written < GetElementCount(chunkIndex))
        {
            (_elementsWritten ??= [])[chunkIndex] = written;
            return;
        }

        _elementsWritten?.Remove(chunkIndex);

        // A cache that cannot give the chunk up keeps it until it is flushed.
        if (!_writingChunkCache.TryRemove(chunkIndex, out var chunk))
            return;

        chunkStream.Close();
        WriteAndReleaseChunk(chunkIndex, chunk);
    }

    public void FlushChunkCache()
    {
        if (WriteContext is not null)
        {
            _writingChunkCache.Flush(WriteAndReleaseChunk);
        }
    }

    /// <summary>
    /// Whether a chunk can be written straight to the file, and where.
    /// </summary>
    protected virtual bool TryGetWriteThroughAddress(ulong chunkIndex, out ulong address)
    {
        address = default;
        return false;
    }

    /// <summary>
    /// How many of a chunk's elements lie inside the dataset: all of them, except in a chunk that overhangs
    /// the end of a dimension.
    /// </summary>
    private ulong GetElementCount(ulong chunkIndex)
    {
        var count = 1UL;

        for (int dimension = 0; dimension < ChunkRank; dimension++)
        {
            var scaledOffset = chunkIndex / DownChunkCounts[dimension] % ScaledDims[dimension];
            var start = scaledOffset * ChunkDims[dimension];

            count *= Math.Min(ChunkDims[dimension], Dims[dimension] - start);
        }

        return count;
    }

    private Memory<byte> AllocateChunk()
    {
        if (!_poolChunks)
            return new byte[ChunkByteSize];

        // Cleared because a pooled array holds whatever its last user left, and an element nobody writes
        // must read back as zero, as it would from a new array.
        var array = ArrayPool<byte>.Shared.Rent((int)ChunkByteSize);
        var chunk = array.AsMemory(0, (int)ChunkByteSize);

        chunk.Span.Clear();

        return chunk;
    }

    private void WriteAndReleaseChunk(ulong chunkIndex, Memory<byte> chunk)
    {
        WriteChunk(chunkIndex, chunk);
        (_finishedChunks ??= []).Add(chunkIndex);

        if (_poolChunks && MemoryMarshal.TryGetArray<byte>(chunk, out var segment) && segment.Array is not null)
            ArrayPool<byte>.Shared.Return(segment.Array);
    }

    /// <summary>
    /// A buffered chunk's write stream, which refuses writes once its chunk has been written - after that,
    /// its buffer may already belong to someone else.
    /// </summary>
    private sealed class ChunkWriteStream(Memory<byte> chunk) : IH5WriteStream
    {
        private readonly SystemMemoryStream _inner = new(chunk);
        private bool _closed;

        public long Position => _inner.Position;

        public void Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public void WriteDataset(Span<byte> buffer)
        {
            if (_closed)
                throw new InvalidOperationException(
                    "This chunk has already been written. Each element of a chunked dataset can be written once.");

            _inner.WriteDataset(buffer);
        }

        public void Close() => _closed = true;

        public void Dispose()
        {
        }
    }

    protected abstract ulong[] GetRawChunkDims();

    protected abstract ValueTask<ChunkInfo> GetReadChunkInfo(ulong chunkIndex);

    protected abstract ChunkInfo GetWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        FlushChunkCache();
    }

    private async ValueTask<Memory<byte>> ReadChunk(
        ulong chunkIndex)
    {
        Memory<byte> chunk;

        // TODO: This way, fill values will become part of the cache
        if (_readingChunkIndexAddressIsUndefined)
        {
            if (Dataset.FillValue.Value is null)
            {
                chunk = new byte[ChunkByteSize];
            }

            else
            {
                chunk = GC
                    .AllocateUninitializedArray<byte>((int)ChunkByteSize);

                chunk.Span.Fill(Dataset.FillValue.Value);
            }
        }

        else
        {
            var chunkInfo = await GetReadChunkInfo(chunkIndex).ConfigureAwait(false);

            if (ReadContext.Superblock.IsUndefinedAddress(chunkInfo.Address))
            {
                if (Dataset.FillValue.Value is null)
                {
                    chunk = new byte[ChunkByteSize];
                }

                else
                {
                    chunk = GC
                        .AllocateUninitializedArray<byte>((int)ChunkByteSize);

                    chunk.Span.Fill(Dataset.FillValue.Value);
                }
            }

            else
            {
                if (Dataset.FilterPipeline is null)
                {
                    chunk = GC
                        .AllocateUninitializedArray<byte>((int)ChunkByteSize);

                    ReadContext.Driver.SeekRelativeToBaseAddress((long)chunkInfo.Address);
                    await ReadContext.Driver.ReadDatasetAsync(chunk).ConfigureAwait(false);
                }

                else
                {
                    var rawChunkSize = (int)chunkInfo.Size;
                    using var filterBufferOwner = new ScratchBuffer<byte>(rawChunkSize);
                    var buffer = filterBufferOwner.Memory[0..rawChunkSize];

                    ReadContext.Driver.SeekRelativeToBaseAddress((long)chunkInfo.Address);
                    await ReadContext.Driver.ReadDatasetAsync(buffer).ConfigureAwait(false);

                    chunk = H5Filter.ExecutePipeline(
                        Dataset.FilterPipeline.FilterDescriptions,
                        chunkInfo.FilterMask,
                        H5FilterFlags.Decompress,
                        (int)ChunkByteSize,
                        buffer);
                }
            }
        }

        return chunk;
    }

    private void WriteChunk(
        ulong chunkIndex,
        Memory<byte> chunk)
    {
        if (Dataset.FilterPipeline is null)
        {
            var chunkInfo = GetWriteChunkInfo(chunkIndex, (uint)chunk.Length, 0);

            WriteContext.Driver.SeekRelativeToBaseAddress((long)chunkInfo.Address);
            WriteContext.Driver.Write(chunk.Span);
        }

        else
        {
            var filterMask = 0U;

            var buffer = H5Filter.ExecutePipeline(
                Dataset.FilterPipeline.FilterDescriptions,
                filterMask,
                H5FilterFlags.None,
                (int)ChunkByteSize,
                chunk
            );

            var chunkInfo = GetWriteChunkInfo(chunkIndex, (uint)buffer.Length, filterMask);

            WriteContext.Driver.SeekRelativeToBaseAddress((long)chunkInfo.Address);
            WriteContext.Driver.Write(buffer.Span);
        }
    }

    #endregion
}