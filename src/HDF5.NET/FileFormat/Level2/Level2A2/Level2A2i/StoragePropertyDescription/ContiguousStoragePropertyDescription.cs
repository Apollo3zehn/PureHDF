namespace HDF5.NET
{
    public class ContiguousStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ContiguousStoragePropertyDescription(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Address = superblock.ReadOffset(reader);
            this.Size = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Size { get; set; }

        #endregion
    }
}
