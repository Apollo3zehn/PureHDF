using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

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
                chunkCacheFactory = this.Dataset.File.ChunkCacheFactory;

            if (chunkCacheFactory is null)
                chunkCacheFactory = H5File.DefaultChunkCacheFactory;

            _chunkCache = chunkCacheFactory();

            _indexAddressIsUndefined = dataset.Context.Superblock.IsUndefinedAddress(dataset.DataLayout.Address);
        }

        #endregion

        #region Properties

        public ulong[] RawChunkDims { get; private set; }

        public ulong[] ChunkDims { get; private set; }

        public byte ChunkRank { get; private set; }

        public ulong ChunkByteSize { get; private set; }

        public ulong[] DatasetDims { get; private set; }

        public ulong[]? DatasetMaxDims { get; private set; }

        public ulong[] ScaledDatasetDims { get; private set; }

        public ulong[] ScaledDatasetMaxDims { get; private set; }

        public ulong TotalScaledDatasetDims { get; private set; }

        public ulong TotalScaledDatasetMaxDims { get; private set; }

        #endregion

        #region Methods

        public static H5D_Chunk Create(H5Dataset dataset, H5DatasetAccess datasetAccess)
        {
            return dataset.DataLayout switch
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

                _                                   => throw new Exception($"Data layout message type '{dataset.DataLayout.GetType().Name}' is not supported.")
            };
        }

        public override void Initialize()
        {
            this.RawChunkDims = this.GetRawChunkDims();
            this.ChunkDims = this.RawChunkDims[..^1].ToArray();
            this.ChunkRank = (byte)this.ChunkDims.Length;
            this.ChunkByteSize = H5Utils.CalculateSize(this.ChunkDims) * this.Dataset.Datatype.Size;
            this.DatasetDims = this.Dataset.Dataspace.DimensionSizes;
            this.DatasetMaxDims = this.Dataset.Dataspace.DimensionMaxSizes;
            this.TotalScaledDatasetDims = 1;
            this.TotalScaledDatasetMaxDims = 1;

            this.ScaledDatasetDims = new ulong[this.ChunkRank];

            for (int i = 0; i < this.ChunkRank; i++)
            {
                this.ScaledDatasetDims[i] = H5Utils.CeilDiv(this.DatasetDims[i], this.ChunkDims[i]);
            }
        }

        public override ulong[] GetChunkDims()
        {
            return this.ChunkDims;
        }

        public override Memory<byte> GetBuffer(ulong[] chunkIndices)
        {
            return _chunkCache.GetChunk(chunkIndices, () => this.ReadChunk(chunkIndices));
        }

        public override Stream GetStream(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        protected abstract ulong[] GetRawChunkDims();

        protected abstract ChunkInfo GetChunkInfo(ulong[] chunkIndices);

        private byte[] ReadChunk(ulong[] chunkIndices)
        {
            var buffer = new byte[this.ChunkByteSize];

#warning This way, fill values will become part of the cache
            if (_indexAddressIsUndefined)
            {
                if (this.Dataset.FillValue.IsDefined)
                    buffer.AsSpan().Fill(this.Dataset.FillValue.Value);
            }
            else
            {
                var chunkInfo = this.GetChunkInfo(chunkIndices);
                this.Dataset.Context.Reader.Seek((long)chunkInfo.Address, SeekOrigin.Begin);
                this.ReadChunk(buffer, chunkInfo.Size, chunkInfo.FilterMask);
            }

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadChunk(Memory<byte> buffer, ulong rawChunkSize, uint filterMask)
        {
            if (this.Dataset.FilterPipeline is null)
            {
                this.Dataset.Context.Reader.Read(buffer.Span);
            }
            else
            {
                using var filterBufferOwner = MemoryPool<byte>.Shared.Rent((int)rawChunkSize);
                var filterBuffer = filterBufferOwner.Memory[0..(int)rawChunkSize];
                this.Dataset.Context.Reader.Read(filterBuffer.Span);

                H5Filter.ExecutePipeline(this.Dataset.FilterPipeline.FilterDescriptions, filterMask, ExtendedFilterFlags.Reverse, filterBuffer, buffer);
            }
        }

        #endregion
    }
}
