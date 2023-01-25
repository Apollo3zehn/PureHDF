namespace HDF5.NET
{
    internal class UserDefinedLinkInfo : LinkInfo
    {
        #region Constructors

        public UserDefinedLinkInfo(H5BaseReader reader)
        {
            // data length
            DataLength = reader.ReadUInt16();

            // data
            Data = reader.ReadBytes(DataLength);
        }

        #endregion

        #region Properties

        public ushort DataLength { get; set; }

        public byte[] Data { get; set; }

        #endregion
    }
}
