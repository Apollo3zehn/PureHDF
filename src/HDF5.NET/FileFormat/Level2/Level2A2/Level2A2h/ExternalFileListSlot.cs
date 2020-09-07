namespace HDF5.NET
{
    public class ExternalFileListSlot : FileBlock
    {
        #region Constructors

        public ExternalFileListSlot(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.LocalHeapNameOffset = superblock.ReadLength(reader);
            this.ExternalDataFileOffset = superblock.ReadLength(reader);
            this.ExternalFileDataSize = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong LocalHeapNameOffset { get; set; }
        public ulong ExternalDataFileOffset { get; set; }
        public ulong ExternalFileDataSize { get; set; }

        #endregion
    }
}
