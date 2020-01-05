using System.Collections.Generic;

namespace HDF5.NET
{
    public class CompoundPropertyDescription1 : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription1()
        {
            //
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public uint MemberByteOffset { get; set; }
        public byte Dimensionality { get; set; }
        public uint DimensionPermuation { get; set; }
        public List<uint> DimensionSize { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}