using System;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public partial class H5Dataset : H5AttributableObject
    {
        private void ReadBTree2Chunk(Memory<byte> buffer, byte rank, ulong chunkSize, ulong[] indices)
        {
            if (this.FilterPipeline == null)
            {
                // btree2
                Func<BTree2Record10> decodeKey = () => this.DecodeRecord10(rank);
                var btree2 = new BTree2Header<BTree2Record10>(this.Context.Reader, this.Context.Superblock, decodeKey);

                // get record
                var success = btree2.TryFindRecord(out var record, record =>
                {
                    // H5Dbtree2.c (H5D__bt2_compare)
                    return this.VectorCompare(rank, indices, record.ScaledOffsets);
                });

                if (!success)
                    record.Address = Superblock.UndefinedAddress;

                // read data
                this.SeekAndReadChunk(buffer, chunkSize, record.Address);
            }
            else
            {
                // btree2
                var chunkSizeLength = this.ComputeChunkSizeLength(chunkSize);
                Func<BTree2Record11> decodeKey = () => this.DecodeRecord11(rank, chunkSizeLength);
                var btree2 = new BTree2Header<BTree2Record11>(this.Context.Reader, this.Context.Superblock, decodeKey);

                // get record
                var success = btree2.TryFindRecord(out var record, record =>
                {
                    // H5Dbtree2.c (H5D__bt2_compare)
                    return this.VectorCompare(rank, indices, record.ScaledOffsets);
                });

                if (!success)
                    record.Address = Superblock.UndefinedAddress;

                // read data
                this.SeekAndReadChunk(buffer, record.ChunkSize, record.Address);
            }
        }

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record10 DecodeRecord10(byte rank)
        {
            return new BTree2Record10(this.Context.Reader, this.Context.Superblock, rank);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record11 DecodeRecord11(byte rank, uint chunkSizeLength)
        {
            return new BTree2Record11(this.Context.Reader, this.Context.Superblock, rank, chunkSizeLength);
        }

        #endregion
    }
}
