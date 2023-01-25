﻿namespace PureHDF
{
    internal class CompactStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public CompactStoragePropertyDescription(H5BaseReader reader)
        {
            // size
            Size = reader.ReadUInt16();

            // raw data
            RawData = reader.ReadBytes(Size);
        }

        #endregion

        #region Properties

        public ushort Size { get; set; }
        public byte[] RawData { get; set; }

        #endregion
    }
}
