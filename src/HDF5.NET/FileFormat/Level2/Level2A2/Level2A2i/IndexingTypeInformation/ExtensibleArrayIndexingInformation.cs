using System;

namespace HDF5.NET
{
    internal class ExtensibleArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public ExtensibleArrayIndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // max bit count
            this.MaxBitCount = reader.ReadByte();

            if (this.MaxBitCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // index element count
            this.IndexElementsCount = reader.ReadByte();

            if (this.IndexElementsCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min pointer count
            this.MinPointerCount = reader.ReadByte();

            if (this.MinPointerCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min element count
            this.MinElementsCount = reader.ReadByte();

            if (this.MinElementsCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // page bit count
            this.PageBitCount = reader.ReadByte();

            if (this.PageBitCount == 0)
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
