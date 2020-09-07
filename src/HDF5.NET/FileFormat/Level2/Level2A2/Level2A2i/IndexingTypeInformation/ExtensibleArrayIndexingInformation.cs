namespace HDF5.NET
{
    public class ExtensibleArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public ExtensibleArrayIndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // max bit count
            this.MaxBitCount = reader.ReadByte();

            // index element count
            this.IndexElementCount = reader.ReadByte();

            // min pointer count
            this.MinPointerCount = reader.ReadByte();

            // min element count
            this.MinElementCount = reader.ReadByte();

            // page bit count
            this.PageBitCount = reader.ReadUInt16();
        }

        #endregion

        #region Properties

        public byte MaxBitCount { get; set; }
        public byte IndexElementCount { get; set; }
        public byte MinPointerCount { get; set; }
        public byte MinElementCount { get; set; }
        public ushort PageBitCount { get; set; }

        #endregion
    }
}
