namespace HDF5.NET
{
    internal class ExtensibleArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public ExtensibleArrayIndexingInformation(H5BinaryReader reader)
        {
            // max bit count
            MaxBitCount = reader.ReadByte();

            if (MaxBitCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // index element count
            IndexElementsCount = reader.ReadByte();

            if (IndexElementsCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min pointer count
            MinPointerCount = reader.ReadByte();

            if (MinPointerCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min element count
            MinElementsCount = reader.ReadByte();

            if (MinElementsCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // page bit count
            PageBitCount = reader.ReadByte();

            if (PageBitCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");
        }

        #endregion

        #region Properties

        public byte MaxBitCount { get; set; }
        public byte IndexElementsCount { get; set; }
        public byte MinPointerCount { get; set; }
        public byte MinElementsCount { get; set; }
        public ushort PageBitCount { get; set; }

        #endregion
    }
}
