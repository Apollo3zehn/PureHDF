using System.Buffers;

namespace PureHDF;

internal abstract class H5D_Chunk : H5D_Base
{
    #region Types

    protected record ChunkInfo(ulong Address, ulong Size, uint FilterMask)
    {
        public static ChunkInfo None { get; } = new ChunkInfo(Superblock.UndefinedAddress, 0, 0);
    }

    #endregion

    #region Fields

    private readonly IReadingChunkCache _readingChunkCache = default!;
    private readonly IWritingChunkCache _writingChunkCache = default!;
    private readonly bool _readingChunkIndexAddressIsUndefined;

    #endregion

    #region Constructors

    public H5D_Chunk(
        NativeReadContext readContext, 
        NativeWriteContext writeContext, 
        DatasetInfo dataset, 
        H5DatasetAccess datasetAccess)
        : base(readContext, writeContext, dataset, datasetAccess)
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        if (readContext is null)
        {
            _writingChunkCache = datasetAccess.WritingChunkCache ?? (writeContext.WriteOptions.ChunkCacheFactory ?? H5WriteOptions.DefaultChunkCacheFactory)();
        }

        else
        {
            _readingChunkCache = datasetAccess.ReadingChunkCache ?? readContext.File.ChunkCacheFactory();

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

    #endregion

    #region Methods

    public static H5D_Chunk Create(
        NativeReadContext readContext, 
        NativeWriteContext writeContext, 
        DatasetInfo dataset, 
        H5DatasetAccess datasetAccess)
    {
        return dataset.Layout switch
        {
            DataLayoutMessage12 layout12 => new H5D_Chunk123_BTree1(readContext, writeContext, dataset, layout12, datasetAccess),

            DataLayoutMessage4 layout4 => ((ChunkedStoragePropertyDescription4)layout4.Properties).IndexingInformation switch
            {
                // the current, maximum, and chunk dimension sizes are all the same
                SingleChunkIndexingInformation => new H5Dataset_Chunk_Single_Chunk4(readContext, writeContext, dataset, layout4, datasetAccess),

                // fixed maximum dimension sizes
                // no filter applied to the dataset
                // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                ImplicitIndexingInformation => new H5D_Chunk4_Implicit(readContext, writeContext, dataset, layout4, datasetAccess),

                // fixed maximum dimension sizes
                FixedArrayIndexingInformation => new H5D_Chunk4_FixedArray(readContext, writeContext, dataset, layout4, datasetAccess),

                // only one dimension of unlimited extent
                ExtensibleArrayIndexingInformation => new H5D_Chunk4_ExtensibleArray(readContext, writeContext, dataset, layout4, datasetAccess),

                // more than one dimension of unlimited extent
                BTree2IndexingInformation => new H5D_Chunk4_BTree2(readContext, writeContext, dataset, layout4, datasetAccess),
                _ => throw new Exception("Unknown chunk indexing type.")
            },

            DataLayoutMessage3 layout3 => new H5D_Chunk123_BTree1(readContext, writeContext, dataset, layout3, datasetAccess),

            _ => throw new Exception($"Data layout message type '{dataset.Layout.GetType().Name}' is not supported.")
        };
    }

    public override void Initialize()
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        RawChunkDims = GetRawChunkDims();
        ChunkDims = RawChunkDims[..^1].ToArray();
        ChunkRank = (byte)ChunkDims.Length;
        ChunkByteSize = MathUtils.CalculateSize(ChunkDims) * Dataset.Type.Size;
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

    public override async Task<IH5ReadStream> GetReadStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
    {
        var buffer = await _readingChunkCache

            .GetChunkAsync(
                chunkIndices, 
                chunkReader: () => ReadChunkAsync(reader, chunkIndices))
                
            .ConfigureAwait(false);

        var stream = new SystemMemoryStream(buffer);

        return stream;
    }

    public override IH5WriteStream GetWriteStream(ulong[] chunkIndices)
    {
        var buffer = _writingChunkCache
            .GetChunk(
                chunkIndices,
                chunkAllocator: () => new byte[ChunkByteSize],
                chunkWriter: WriteChunk);

        var stream = new SystemMemoryStream(buffer);

        return stream;
    }

    protected abstract ulong[] GetRawChunkDims();

    protected abstract ChunkInfo GetReadChunkInfo(ulong[] chunkIndices);

    protected abstract ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize, uint filterMask);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (WriteContext is not null)
            _writingChunkCache.Flush(WriteChunk);
    }

    private async Task<Memory<byte>> ReadChunkAsync<TReader>(
        TReader reader, 
        ulong[] chunkIndices) 
        where TReader : IReader
    {
        Memory<byte> chunk;

        // TODO: This way, fill values will become part of the cache
        if (_readingChunkIndexAddressIsUndefined)
        {
            chunk = new byte[ChunkByteSize];

            if (Dataset.FillValue.Value is not null)
                chunk.Span.Fill(Dataset.FillValue.Value);
        }

        else
        {
            var chunkInfo = GetReadChunkInfo(chunkIndices);

            if (ReadContext.Superblock.IsUndefinedAddress(chunkInfo.Address))
            {
                chunk = new byte[ChunkByteSize];

                if (Dataset.FillValue.Value is not null)
                    chunk.Span.Fill(Dataset.FillValue.Value);
            }

            else
            {
                if (Dataset.FilterPipeline is null)
                {
                    chunk = new byte[ChunkByteSize];

                    await reader
                        .ReadDatasetAsync(ReadContext.Driver, chunk, (long)chunkInfo.Address)
                        .ConfigureAwait(false);
                }
                
                else
                {
                    var rawChunkSize = (int)chunkInfo.Size;
                    using var filterBufferOwner = MemoryPool<byte>.Shared.Rent(rawChunkSize);
                    var buffer = filterBufferOwner.Memory[0..rawChunkSize];

                    await reader
                        .ReadDatasetAsync(ReadContext.Driver, buffer, (long)chunkInfo.Address)
                        .ConfigureAwait(false);

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
        ulong[] chunkIndices, 
        Memory<byte> chunk)
    {
        if (Dataset.FilterPipeline is null)
        {
            var chunkInfo = GetWriteChunkInfo(chunkIndices, (uint)chunk.Length, 0);

            WriteContext.Driver.Seek((long)chunkInfo.Address, SeekOrigin.Begin);
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
                chunk);

            var chunkInfo = GetWriteChunkInfo(chunkIndices, (uint)buffer.Length, filterMask);

            WriteContext.Driver.Seek((long)chunkInfo.Address, SeekOrigin.Begin);
            WriteContext.Driver.Write(buffer.Span);
        }
    }

    #endregion
}