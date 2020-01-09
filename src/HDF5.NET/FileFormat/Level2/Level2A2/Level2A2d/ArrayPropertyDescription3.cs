using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class ArrayPropertyDescription3 : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription3(BinaryReader reader) : base(reader)
        {
            // dimensionality
            this.Dimensionality = reader.ReadByte();

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

        public byte Version { get; set; }
        public byte Dimensionality { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}