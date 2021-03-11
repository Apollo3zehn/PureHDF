using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal class H5D_Chunk4_BTree2 : 
        H5D_Chunk4
    {
        #region Fields

        private BTree2Header<BTree2Record10> _btree2_no_filter;
        private BTree2Header<BTree2Record11> _btree2_filter;

        #endregion

        #region Constructors
        public H5D_Chunk4_BTree2(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) 
            : base(dataset, layout, datasetAccess)
        {
            
        }

        #endregion

        #region Methods

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            if (this.Dataset.FilterPipeline is null)
            {
                if (_btree2_no_filter is null)
                {
                    this.Dataset.Context.Reader.Seek((long)this.Dataset.DataLayout.Address, SeekOrigin.Begin);

                    Func<BTree2Record10> decodeKey 
                        = () => this.DecodeRecord10(this.ChunkRank);

                    _btree2_no_filter = new BTree2Header<BTree2Record10>(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, decodeKey);
                }

                // get record
                var success = _btree2_no_filter.TryFindRecord(out var record, record =>
                {
                    // H5Dbtree2.c (H5D__bt2_compare)
                    return H5Utils.VectorCompare(this.ChunkRank, chunkIndices, record.ScaledOffsets);
                });

                return success
                    ? new ChunkInfo(record.Address, this.ChunkByteSize, 0)
                    : ChunkInfo.None;
            }
            else
            {
                if (_btree2_filter is null)
                {
                    this.Dataset.Context.Reader.Seek((long)this.Dataset.DataLayout.Address, SeekOrigin.Begin);
                    var chunkSizeLength = H5Utils.ComputeChunkSizeLength(this.ChunkByteSize);

                    Func<BTree2Record11> decodeKey = 
                        () => this.DecodeRecord11(this.ChunkRank, chunkSizeLength);

                    _btree2_filter = new BTree2Header<BTree2Record11>(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, decodeKey);
                }

                // get record
                var success = _btree2_filter.TryFindRecord(out var record, record =>
                {
                    // H5Dbtree2.c (H5D__bt2_compare)
                    return H5Utils.VectorCompare(this.ChunkRank, chunkIndices, record.ScaledOffsets);
                });

                return success
                    ? new ChunkInfo(record.Address, record.ChunkSize, record.FilterMask)
                    : ChunkInfo.None;
            }
        }

        #endregion

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record10 DecodeRecord10(byte rank)
        {
            return new BTree2Record10(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, rank);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record11 DecodeRecord11(byte rank, uint chunkSizeLength)
        {
            return new BTree2Record11(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, rank, chunkSizeLength);
        }

        #endregion
    }
}
