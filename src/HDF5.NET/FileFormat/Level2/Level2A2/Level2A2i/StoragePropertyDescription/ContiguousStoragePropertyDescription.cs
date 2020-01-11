using System.IO;

namespace HDF5.NET
{
    public class ContiguousStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ContiguousStoragePropertyDescription(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Address = superblock.ReadOffset();
            this.Size = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Size { get; set; }

        #endregion
    }
}
