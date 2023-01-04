using System.Buffers;

namespace HDF5.NET
{
    internal abstract class H5D_Chunk : H5D_Base
    {
        #region Types

        protected record ChunkInfo(ulong Address, ulong Size, uint FilterMask)
        {
            public static ChunkInfo None { get; } = new ChunkInfo(Superblock.UndefinedAddress, 0, 0);
        }

        #endregion

        #region Fields

        private IChunkCache _chunkCache;
        private bool _indexAddressIsUndefined;

        #endregion

        #region Constructors

        public H5D_Chunk(H5Dataset dataset, H5DatasetAccess datasetAccess) :
           base(dataset, supportsBuffer: true, supportsStream: false, datasetAccess)
        {
            // H5Dchunk.c (H5D__chunk_set_info_real)

            var chunkCacheFactory = datasetAccess.ChunkCacheFactory;

            if (chunkCacheFactory is null)
                chunkCacheFactory = Dataset.File.ChunkCacheFactory;

            if (chunkCacheFactory is null)
                chunkCacheFactory = H5File.DefaultChunkCacheFactory;

            _chunkCache = chunkCacheFactory();

            _indexAddressIsUndefined = dataset.Context.Superblock.IsUndefinedAddress(dataset.InternalDataLayout.Address);
        }

        #endregion

        #region Properties

        public ulong[] RawChunkDims { get; private set; }

        public ulong[] ChunkDims { get; private set; }

        public byte ChunkRank { get; private set; }

        public ulong ChunkByteSize { get; private set; }

        public ulong[] Dims { get; private set; }

        public ulong[] MaxDims { get; private set; }

        public ulong[] ScaledDims { get; private set; }

        public ulong[] ScaledMaxDims { get; private set; }

        public ulong[] DownChunkCounts { get; private set; }

        public ulong[] DownMaxChunkCounts { get; private set; }

        public ulong TotalChunkCount { get; private set; }

        public ulong TotalMaxChunkCount { get; private set; }

        #endregion

        #region Methods

        public static H5D_Chunk Create(H5Dataset dataset, H5DatasetAccess datasetAccess)
        {
            return dataset.InternalDataLayout switch
            {
                DataLayoutMessage12 layout12        => new H5D_Chunk123_BTree1(dataset, layout12, datasetAccess),

                DataLayoutMessage4 layout4          => ((ChunkedStoragePropertyDescription4)layout4.Properties).ChunkIndexingType switch
                {
                    // the current, maximum, and chunk dimension sizes are all the same
                    ChunkIndexingType.SingleChunk       => new H5Dataset_Chunk_Single_Chunk4(dataset, layout4, datasetAccess),

                    // fixed maximum dimension sizes
                    // no filter applied to the dataset
                    // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                    ChunkIndexingType.Implicit          => new H5D_Chunk4_Implicit(dataset, layout4, datasetAccess),

                    // fixed maximum dimension sizes
                    ChunkIndexingType.FixedArray        => new H5D_Chunk4_FixedArray(dataset, layout4, datasetAccess),

                    // only one dimension of unlimited extent
                    ChunkIndexingType.ExtensibleArray   => new H5D_Chunk4_ExtensibleArray(dataset, layout4, datasetAccess),

                    // more than one dimension of unlimited extent
                    ChunkIndexingType.BTree2            => new H5D_Chunk4_BTree2(dataset, layout4, datasetAccess),
                    _                                   => throw new Exception("Unknown chunk indexing type.")
                },

                DataLayoutMessage3 layout3          => new H5D_Chunk123_BTree1(dataset, layout3, datasetAccess),

                _                                   => throw new Exception($"Data layout message type '{dataset.InternalDataLayout.GetType().Name}' is not supported.")
            };
        }

        public override void Initialize()
        {
            // H5Dchunk.c (H5D__chunk_set_info_real)

            RawChunkDims = GetRawChunkDims();
            ChunkDims = RawChunkDims[..^1].ToArray();
            ChunkRank = (byte)ChunkDims.Length;
            ChunkByteSize = H5Utils.CalculateSize(ChunkDims) * Dataset.InternalDataType.Size;
            Dims = Dataset.InternalDataspace.DimensionSizes;
            MaxDims = Dataset.InternalDataspace.DimensionMaxSizes;
            TotalChunkCount = 1;
            TotalMaxChunkCount = 1;

            ScaledDims = new ulong[ChunkRank];
            ScaledMaxDims = new ulong[ChunkRank];

            for (int i = 0; i < ChunkRank; i++)
            {
                ScaledDims[i] = H5Utils.CeilDiv(Dims[i], ChunkDims[i]);

                if (MaxDims[i] == H5Constants.Unlimited)
                    ScaledMaxDims[i] = H5Constants.Unlimited;

                else
                    ScaledMaxDims[i] = H5Utils.CeilDiv(MaxDims[i], ChunkDims[i]);

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

        public override Task<Memory<byte>> GetBufferAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            return _chunkCache.GetChunkAsync(chunkIndices, () => ReadChunkAsync(reader, chunkIndices));
        }

        public override Stream? GetStream(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        protected abstract ulong[] GetRawChunkDims();

        protected abstract ChunkInfo GetChunkInfo(ulong[] chunkIndices);

        private async Task<Memory<byte>> ReadChunkAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader
        {
            var buffer = new byte[ChunkByteSize];

#warning This way, fill values will become part of the cache
            if (_indexAddressIsUndefined)
            {
                if (Dataset.InternalFillValue.Value is not null)
                    buffer.AsSpan().Fill(Dataset.InternalFillValue.Value);
            }
            else
            {
                var chunkInfo = GetChunkInfo(chunkIndices);

                if (Dataset.Context.Superblock.IsUndefinedAddress(chunkInfo.Address))
                {
                    if (Dataset.InternalFillValue.Value is not null)
                        buffer.AsSpan().Fill(Dataset.InternalFillValue.Value);
                }
                else
                {
                    Dataset.Context.Reader.Seek((long)chunkInfo.Address, SeekOrigin.Begin);
                    await ReadChunkAsync(reader, buffer, chunkInfo.Size, chunkInfo.FilterMask).ConfigureAwait(false);
                }
            }

            return buffer;
        }

        private async Task ReadChunkAsync<TReader>(TReader reader, Memory<byte> buffer, ulong rawChunkSize, uint filterMask) where TReader : IReader
        {
            if (Dataset.InternalFilterPipeline is null)
            {
                await reader.ReadAsync(Dataset.Context.Reader.BaseStream, buffer).ConfigureAwait(false);
            }
            else
            {
                using var filterBufferOwner = MemoryPool<byte>.Shared.Rent((int)rawChunkSize);
                var filterBuffer = filterBufferOwner.Memory[0..(int)rawChunkSize];
                await reader.ReadAsync(Dataset.Context.Reader.BaseStream, filterBuffer).ConfigureAwait(false);

                H5Filter.ExecutePipeline(Dataset.InternalFilterPipeline.FilterDescriptions, filterMask, H5FilterFlags.Decompress, filterBuffer, buffer);
            }
        }

        #endregion
    }
}
