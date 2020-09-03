using System.IO;

namespace HDF5.NET
{
    public class BTree2Record04 : BTree2Record
    {
        #region Constructors

        public BTree2Record04(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.FilteredHugeObjectAddress = superblock.ReadOffset(reader);
            this.FilteredHugeObjectLength = superblock.ReadLength(reader);
            this.FilterMask = reader.ReadUInt32();
            this.FilteredHugeObjectMemorySize = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong FilteredHugeObjectAddress { get; set; }
        public ulong FilteredHugeObjectLength { get; set; }
        public uint FilterMask { get; set; }
        public ulong FilteredHugeObjectMemorySize { get; set; }

        #endregion
    }
}
