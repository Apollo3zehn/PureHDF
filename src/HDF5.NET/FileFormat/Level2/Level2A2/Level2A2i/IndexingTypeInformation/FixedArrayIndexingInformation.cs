namespace HDF5.NET
{
    internal class FixedArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public FixedArrayIndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // page bits
            PageBits = reader.ReadByte();

            if (PageBits == 0)
                throw new Exception("Invalid fixed array creation parameter.");
        }

        #endregion

        #region Properties

        public byte PageBits { get; set; }

        #endregion
    }
}
