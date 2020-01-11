using System.IO;

namespace HDF5.NET
{
    public class GlobalHeapId : FileBlock
    {
        #region Constructors

        public GlobalHeapId(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.CollectionAddress = superblock.ReadOffset();
            this.ObjectIndex = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public ulong CollectionAddress { get; set; }
        public uint ObjectIndex { get; set; }

        public GlobalHeapCollection Collection
        {
            get
            {
                return new GlobalHeapCollection(this.Reader);
            }
        }

        #endregion
    }
}