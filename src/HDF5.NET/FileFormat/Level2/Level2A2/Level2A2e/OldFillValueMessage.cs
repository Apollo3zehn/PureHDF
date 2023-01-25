namespace HDF5.NET
{
    internal class OldFillValueMessage : Message
    {
        #region Constructors

        public OldFillValueMessage(H5BaseReader reader)
        {
            Size = reader.ReadUInt32();
            FillValue = reader.ReadBytes((int)Size);
        }

        #endregion

        #region Properties

        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
