namespace PureHDF
{
    internal class SoftLinkInfo : LinkInfo
    {
        #region Constructors

        public SoftLinkInfo(H5DriverBase driver)
        {
            // value length
            ValueLength = driver.ReadUInt16();

            // value
            Value = ReadUtils.ReadFixedLengthString(driver, ValueLength);
        }

        #endregion

        #region Properties

        public ushort ValueLength { get; set; }
        public string Value { get; set; }

        #endregion
    }
}
