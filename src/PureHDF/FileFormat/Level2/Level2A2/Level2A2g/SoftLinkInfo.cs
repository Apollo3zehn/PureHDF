namespace PureHDF
{
    internal class SoftLinkInfo : LinkInfo
    {
        #region Constructors

        public SoftLinkInfo(H5BaseReader reader)
        {
            // value length
            ValueLength = reader.ReadUInt16();

            // value
            Value = ReadUtils.ReadFixedLengthString(reader, ValueLength);
        }

        #endregion

        #region Properties

        public ushort ValueLength { get; set; }
        public string Value { get; set; }

        #endregion
    }
}
