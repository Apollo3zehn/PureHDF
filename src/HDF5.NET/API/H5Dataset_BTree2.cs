using System;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public partial class H5Dataset
    {
        private void ReadBTree2Chunk(Memory<byte> buffer, byte rank, ulong chunkSize, ulong[] chunkIndices)
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
                    return this.VectorCompare(rank, chunkIndices, record.ScaledOffsets);
                });

                // read data
                if (success)
                {
                    this.SeekAndReadChunk(buffer, chunkSize, 0, record.Address);
                }
                else
                {
                    if (this.FillValue.IsDefined)
                        buffer.Span.Fill(this.FillValue.Value);
                }
            }
            else
            {
                // btree2
                var chunkSizeLength = H5Utils.ComputeChunkSizeLength(chunkSize);
                Func<BTree2Record11> decodeKey = () => this.DecodeRecord11(rank, chunkSizeLength);
                var btree2 = new BTree2Header<BTree2Record11>(this.Context.Reader, this.Context.Superblock, decodeKey);

                // get record
                var success = btree2.TryFindRecord(out var record, record =>
                {
                    // H5Dbtree2.c (H5D__bt2_compare)
                    return this.VectorCompare(rank, chunkIndices, record.ScaledOffsets);
                });

                // read data
                if (success)
                {
                    this.SeekAndReadChunk(buffer, record.ChunkSize, record.FilterMask, record.Address);
                }
                else
                {
                    if (this.FillValue.IsDefined)
                        buffer.Span.Fill(this.FillValue.Value);
                }
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
