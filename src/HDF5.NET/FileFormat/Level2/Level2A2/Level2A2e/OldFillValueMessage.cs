namespace HDF5.NET
{
    public class OldFillValueMessage : Message
    {
        #region Constructors

        public OldFillValueMessage(H5BinaryReader reader) : base(reader)
        {
            this.Size = reader.ReadUInt32();
            this.FillValue = reader.ReadBytes((int)this.Size);
        }

        #endregion

        #region Properties

        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
