using System.Collections.Generic;

namespace HDF5.NET
{
    public struct BTree2Record11 : IBTree2Record
    {
        #region Constructors

        public BTree2Record11(H5BinaryReader reader, Superblock superblock, byte dimensionality, uint chunkSizeLength)
        {
            // address
            this.Address = superblock.ReadOffset(reader);

            // chunk size
            this.ChunkSize = H5Utils.ReadUlong(reader, chunkSizeLength);

            // filter mask
            this.FilterMask = reader.ReadUInt32();

            // scaled offsets
            this.ScaledOffsets = new ulong[dimensionality];

            for (int i = 0; i < dimensionality; i++)
            {
                this.ScaledOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public ulong[] ScaledOffsets { get; set; }

        #endregion
    }
}
