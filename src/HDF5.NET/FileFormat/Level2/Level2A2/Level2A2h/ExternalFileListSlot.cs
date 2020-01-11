using System.IO;

namespace HDF5.NET
{
    public class ExternalFileListSlot : FileBlock
    {
        #region Constructors

        public ExternalFileListSlot(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.LocalHeapNameOffset = superblock.ReadLength();
            this.ExternalDataFileOffset = superblock.ReadLength();
            this.ExternalFileDataSize = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong LocalHeapNameOffset { get; set; }
        public ulong ExternalDataFileOffset { get; set; }
        public ulong ExternalFileDataSize { get; set; }

        #endregion
    }
}
