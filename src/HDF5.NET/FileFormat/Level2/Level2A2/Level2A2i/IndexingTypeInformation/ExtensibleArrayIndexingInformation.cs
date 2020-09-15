using System;

namespace HDF5.NET
{
    public class ExtensibleArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public ExtensibleArrayIndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // max bit count
            this.MaxBitCount = reader.ReadByte();

            if (this.MaxBitCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // index element count
            this.IndexElementCount = reader.ReadByte();

            if (this.IndexElementCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min pointer count
            this.MinPointerCount = reader.ReadByte();

            if (this.MinPointerCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // min element count
            this.MinElementCount = reader.ReadByte();

            if (this.MinElementCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");

            // page bit count
            this.PageBitCount = reader.ReadByte();

            if (this.PageBitCount == 0)
                throw new Exception("Invalid extensible array creation parameter.");
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
