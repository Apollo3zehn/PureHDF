namespace PureHDF
{
    internal class CompactStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public CompactStoragePropertyDescription(H5DriverBase driver)
        {
            // size
            Size = driver.ReadUInt16();

            // raw data
            RawData = driver.ReadBytes(Size);
        }

        #endregion

        #region Properties

        public ushort Size { get; set; }
        public byte[] RawData { get; set; }

        #endregion
    }
}
