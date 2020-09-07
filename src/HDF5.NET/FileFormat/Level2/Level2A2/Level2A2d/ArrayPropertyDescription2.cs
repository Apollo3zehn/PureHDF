using System.Collections.Generic;

namespace HDF5.NET
{
    public class ArrayPropertyDescription2 : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription2(H5BinaryReader reader) : base(reader)
        {
            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // dimension sizes
            this.DimensionSizes = new List<uint>(this.Dimensionality);

            for (int i = 0; i < this.Dimensionality; i++)
            {
                this.DimensionSizes.Add(reader.ReadUInt32());
            }

            // base type
            this.BaseType = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}