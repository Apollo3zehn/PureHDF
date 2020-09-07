namespace HDF5.NET
{
    public class FixedArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public FixedArrayIndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // page bit count
            this.PageBitCount = reader.ReadByte();
        }

        #endregion

        #region Properties

        public byte PageBitCount { get; set; }

        #endregion
    }
}
