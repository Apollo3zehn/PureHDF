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

    private readonly IChunkCache _chunkCache;
    private readonly bool _indexAddressIsUndefined;

    #endregion

    #region Constructors

    public H5D_Chunk(NativeDataset dataset, H5DatasetAccess datasetAccess) :
       base(dataset, datasetAccess)
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        var chunkCacheFactory = datasetAccess.ChunkCacheFactory;

        chunkCacheFactory ??= ((NativeFile)Dataset.File).ChunkCacheFactory;

        _chunkCache = chunkCacheFactory();

        _indexAddressIsUndefined = dataset.Context.Superblock.IsUndefinedAddress(dataset.DataLayoutMessage.Address);
    }

    #endregion

    #region Properties

    // these properties will all be initialized in Initialize()

    public ulong[] RawChunkDims { get; private set; } = default!;

    public ulong[] ChunkDims { get; private set; } = default!;

    public byte ChunkRank { get; private set; }

    public ulong ChunkByteSize { get; private set; }

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

    public static H5D_Chunk Create(NativeDataset dataset, H5DatasetAccess datasetAccess)
    {
        return dataset.DataLayoutMessage switch
        {
            DataLayoutMessage12 layout12 => new H5D_Chunk123_BTree1(dataset, layout12, datasetAccess),

            DataLayoutMessage4 layout4 => ((ChunkedStoragePropertyDescription4)layout4.Properties).ChunkIndexingType switch
            {
                // the current, maximum, and chunk dimension sizes are all the same
                ChunkIndexingType.SingleChunk => new H5Dataset_Chunk_Single_Chunk4(dataset, layout4, datasetAccess),

                // fixed maximum dimension sizes
                // no filter applied to the dataset
                // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                ChunkIndexingType.Implicit => new H5D_Chunk4_Implicit(dataset, layout4, datasetAccess),

                // fixed maximum dimension sizes
                ChunkIndexingType.FixedArray => new H5D_Chunk4_FixedArray(dataset, layout4, datasetAccess),

                // only one dimension of unlimited extent
                ChunkIndexingType.ExtensibleArray => new H5D_Chunk4_ExtensibleArray(dataset, layout4, datasetAccess),

                // more than one dimension of unlimited extent
                ChunkIndexingType.BTree2 => new H5D_Chunk4_BTree2(dataset, layout4, datasetAccess),
                _ => throw new Exception("Unknown chunk indexing type.")
            },

            DataLayoutMessage3 layout3 => new H5D_Chunk123_BTree1(dataset, layout3, datasetAccess),

            _ => throw new Exception($"Data layout message type '{dataset.DataLayoutMessage.GetType().Name}' is not supported.")
        };
    }

    public override void Initialize()
    {
        // H5Dchunk.c (H5D__chunk_set_info_real)

        RawChunkDims = GetRawChunkDims();
        ChunkDims = RawChunkDims[..^1].ToArray();
        ChunkRank = (byte)ChunkDims.Length;
        ChunkByteSize = Utils.CalculateSize(ChunkDims) * Dataset.DataTypeMessage.Size;
        Dims = Dataset.DataspaceMessage.DimensionSizes;
        MaxDims = Dataset.DataspaceMessage.DimensionMaxSizes;
        TotalChunkCount = 1;
        TotalMaxChunkCount = 1;

        ScaledDims = new ulong[ChunkRank];
        ScaledMaxDims = new ulong[ChunkRank];

        for (int i = 0; i < ChunkRank; i++)
        {
            ScaledDims[i] = Utils.CeilDiv(Dims[i], ChunkDims[i]);

            if (MaxDims[i] == H5Constants.Unlimited)
                ScaledMaxDims[i] = H5Constants.Unlimited;

            else
                ScaledMaxDims[i] = Utils.CeilDiv(MaxDims[i], ChunkDims[i]);

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

    public override async Task<IH5ReadStream> GetStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
    {
        var buffer = await _chunkCache
            .GetChunkAsync(chunkIndices, () => ReadChunkAsync(reader, chunkIndices))
            .ConfigureAwait(false);

        var stream = new SystemMemoryStream(buffer);

        return stream;
    }

    protected abstract ulong[] GetRawChunkDims();

    protected abstract ChunkInfo GetChunkInfo(ulong[] chunkIndices);

    private async Task<Memory<byte>> ReadChunkAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader
    {
        var buffer = new byte[ChunkByteSize];

        // TODO: This way, fill values will become part of the cache
        if (_indexAddressIsUndefined)
        {
            if (Dataset.FillValueMessage.Value is not null)
                buffer.AsSpan().Fill(Dataset.FillValueMessage.Value);
        }
        else
        {
            var chunkInfo = GetChunkInfo(chunkIndices);

            if (Dataset.Context.Superblock.IsUndefinedAddress(chunkInfo.Address))
            {
                if (Dataset.FillValueMessage.Value is not null)
                    buffer.AsSpan().Fill(Dataset.FillValueMessage.Value);
            }

            else
            {
                await ReadChunkAsync(reader, buffer, (long)chunkInfo.Address, chunkInfo.Size, chunkInfo.FilterMask).ConfigureAwait(false);
            }
        }

        return buffer;
    }

    private async Task ReadChunkAsync<TReader>(
        TReader reader,
        Memory<byte> buffer,
        long offset,
        ulong rawChunkSize,
        uint filterMask) where TReader : IReader
    {
        if (Dataset.InternalFilterPipeline is null)
        {
            await reader.ReadDatasetAsync(Dataset.Context.Driver, buffer, offset).ConfigureAwait(false);
        }
        else
        {
            using var filterBufferOwner = MemoryPool<byte>.Shared.Rent((int)rawChunkSize);
            var filterBuffer = filterBufferOwner.Memory[0..(int)rawChunkSize];
            await reader.ReadDatasetAsync(Dataset.Context.Driver, filterBuffer, offset).ConfigureAwait(false);

            H5Filter.ExecutePipeline(Dataset.InternalFilterPipeline.FilterDescriptions, filterMask, H5FilterFlags.Decompress, filterBuffer, buffer);
        }
    }

    #endregion
}