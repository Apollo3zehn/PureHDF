namespace PureHDF
{
    internal class OldFillValueMessage : Message
    {
        #region Constructors

        public OldFillValueMessage(H5DriverBase driver)
        {
            Size = driver.ReadUInt32();
            FillValue = driver.ReadBytes((int)Size);
        }

        #endregion

        #region Properties

        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
