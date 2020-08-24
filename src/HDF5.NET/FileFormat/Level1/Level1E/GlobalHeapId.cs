using System.IO;

namespace HDF5.NET
{
    public class GlobalHeapId : FileBlock
    {
        #region Fields

        private Superblock _superblock;

        #endregion

        #region Constructors

        public GlobalHeapId(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            this.CollectionAddress = superblock.ReadOffset(reader);
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
#warning Because Global Heap ID gets a brand new reader (from the attribute), it cannot be reused here. Is this a good approach?
                var reader = _superblock.Reader;
                return GlobalHeapCache.GetGlobalHeapObject(reader, _superblock, this.CollectionAddress);
            }
        }

        #endregion
    }
}