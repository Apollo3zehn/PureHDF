using System.Collections.Generic;

namespace HDF5.NET
{
    public class ArrayPropertyDescription2 : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription2()
        {
            //
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public List<uint> PermutationIndices { get; set; }
        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}