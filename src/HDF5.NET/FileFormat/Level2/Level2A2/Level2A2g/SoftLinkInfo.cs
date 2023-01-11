namespace HDF5.NET
{
    internal class SoftLinkInfo : LinkInfo
    {
        #region Constructors

        public SoftLinkInfo(H5BinaryReader reader)
        {
            // value length
            ValueLength = reader.ReadUInt16();

            // value
            Value = H5ReadUtils.ReadFixedLengthString(reader, ValueLength);
        }

        #endregion

        #region Properties

        public ushort ValueLength { get; set; }
        public string Value { get; set; }

        #endregion
    }
}
