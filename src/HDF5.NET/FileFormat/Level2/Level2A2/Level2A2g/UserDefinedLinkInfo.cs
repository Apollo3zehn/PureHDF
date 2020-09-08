namespace HDF5.NET
{
    public class UserDefinedLinkInfo : LinkInfo
    {
        #region Constructors

        public UserDefinedLinkInfo(H5BinaryReader reader) : base(reader)
        {
            // data length
            this.DataLength = reader.ReadUInt16();

            // data
            this.Data = reader.ReadBytes(this.DataLength);
        }

        #endregion

        #region Properties

        public ushort DataLength { get; set; }

        public byte[] Data { get; set; }

        #endregion
    }
}
