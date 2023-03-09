namespace PureHDF
{
    internal class ContiguousStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ContiguousStoragePropertyDescription(H5Context context)
        {
            var (driver, superblock) = context;

            Address = superblock.ReadOffset(driver);
            Size = superblock.ReadLength(driver);
        }

        #endregion

        #region Properties

        public ulong Size { get; set; }

        #endregion
    }
}
