namespace HDF5.NET
{
    internal class ExternalFileListSlot : FileBlock
    {
        #region Constructors

        public ExternalFileListSlot(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // name heap offset
            this.NameHeapOffset = superblock.ReadLength(reader);

            // offset
            this.Offset = superblock.ReadLength(reader);

            // size
            this.Size = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong NameHeapOffset { get; set; }
        public ulong Offset { get; set; }
        public ulong Size { get; set; }

        #endregion
    }
}
