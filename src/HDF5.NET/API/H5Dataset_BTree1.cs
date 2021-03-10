using System;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public partial class H5Dataset
    {
        private void ReadBTree1Chunk(Memory<byte> buffer, byte rank, uint[] dimensionSizes, ulong[] chunkIndices)
        {
            // btree1
            Func<BTree1RawDataChunksKey> decodeKey = () => this.DecodeRawDataChunksKey(rank, dimensionSizes);
            var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.Context.Reader, this.Context.Superblock, decodeKey);

            // get key and child address
            var success = btree1
                        .TryFindUserData(out var userData,
                                        (leftKey, rightKey) => this.NodeCompare3(rank, chunkIndices, leftKey, rightKey),
                                        (ulong address, BTree1RawDataChunksKey leftKey, out BTree1RawDataChunkUserData userData) 
                                            => this.NodeFound(rank, chunkIndices, address, leftKey, out userData));

            // read data
            if (success)
            {
                this.SeekAndReadChunk(buffer, userData.ChunkSize, userData.FilterMask, userData.ChildAddress);
            }
            else
            {
                if (this.FillValue.IsDefined)
                    buffer.Span.Fill(this.FillValue.Value);
            }           
        }

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree1RawDataChunksKey DecodeRawDataChunksKey(byte rank, uint[] dimensionSizes)
        {
            return new BTree1RawDataChunksKey(this.Context.Reader, rank, dimensionSizes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NodeCompare3(byte rank, ulong[] indices, BTree1RawDataChunksKey leftKey, BTree1RawDataChunksKey rightKey)
        {
            // H5Dbtree.c (H5D__btree_cmp3)

            /* Special case for faster checks on 1-D chunks */
            /* (Checking for ndims==2 because last dimension is the datatype size) */
            /* The additional checking for the right key is necessary due to the */
            /* slightly odd way the library initializes the right-most node in the */
            /* indexed storage B-tree... */
            if (rank == 2)
            {
                if (indices[0] >= rightKey.ScaledChunkOffsets[0])
                    return 1;

                /* not sure why original code has another else-if at this point */

                else if (indices[0] < leftKey.ScaledChunkOffsets[0])
                    return -1;
            }

            else
            {
                if (this.VectorCompare(rank, indices, rightKey.ScaledChunkOffsets) >= 0)
                    return 1;

                else if (this.VectorCompare(rank, indices, leftKey.ScaledChunkOffsets) < 0)
                    return -1;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NodeFound(byte rank, ulong[] indices, ulong address, BTree1RawDataChunksKey leftKey, out BTree1RawDataChunkUserData userData)
        {
            // H5Dbtree.c (H5D__btree_found)

            userData = default;

            /* Is this *really* the requested chunk? */
            for (int i = 0; i < rank; i++)
            {
                if (indices[i] >= leftKey.ScaledChunkOffsets[i] + 1)
                    return false;
            }

            userData.ChildAddress = address;
            userData.ChunkSize = leftKey.ChunkSize;
            userData.FilterMask = leftKey.FilterMask;

            return true;
        }

        #endregion
    }
}
