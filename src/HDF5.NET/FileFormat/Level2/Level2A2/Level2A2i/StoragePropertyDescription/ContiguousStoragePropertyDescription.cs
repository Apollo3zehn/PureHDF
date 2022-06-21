namespace HDF5.NET
{
    internal class ContiguousStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ContiguousStoragePropertyDescription(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            Address = superblock.ReadOffset(reader);
            Size = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong Size { get; set; }

        #endregion
    }
}
