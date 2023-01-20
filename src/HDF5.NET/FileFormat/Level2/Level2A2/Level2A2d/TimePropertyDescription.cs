﻿namespace HDF5.NET
{
    internal class TimePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public TimePropertyDescription(H5BinaryReader reader)
        {
            BitPrecision = reader.ReadUInt16();
        }

        #endregion

        #region Properties

        public ushort BitPrecision { get; set; }

        #endregion
    }
}