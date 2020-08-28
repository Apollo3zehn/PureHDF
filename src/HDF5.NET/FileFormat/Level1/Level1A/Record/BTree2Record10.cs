using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class BTree2Record10 : BTree2Record
    {
        #region Constructors

        public BTree2Record10(BinaryReader reader, Superblock superblock, ushort recordSize) : base(reader)
        {
            // address
            this.Address = superblock.ReadOffset();

            // scaled offsets
            var rank = recordSize - superblock.OffsetsSize;
            this.ScaledOffsets = new List<ulong>(rank);

            for (int i = 0; i < rank; i++)
            {
                this.ScaledOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public List<ulong> ScaledOffsets { get; set; }

        #endregion
    }
}
