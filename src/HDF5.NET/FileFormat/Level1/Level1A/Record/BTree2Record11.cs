using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class BTree2Record11 : BTree2Record
    {
        #region Constructors

#warning How to get rank? Is rank correct name?
        public BTree2Record11(BinaryReader reader, Superblock superblock, int rank) : base(reader)
        {
            // address
            this.Address = superblock.ReadOffset();

            // chunk size
#warning how to correctly parse this field?
            this.ChunkSize = reader.ReadByte();

            // filter mask
            this.FilterMask = reader.ReadUInt32();

            // scaled offsets
            this.ScaledOffsets = new List<ulong>(rank);

            for (int i = 0; i < rank; i++)
            {
                this.ScaledOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public List<ulong> ScaledOffsets { get; set; }

        #endregion
    }
}
