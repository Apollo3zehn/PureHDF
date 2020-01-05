using System.Collections.Generic;

namespace HDF5.NET
{
    public class ArrayPropertyDescription3 : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription3()
        {
            //
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public DataypeMessage BaseType { get; set; }

        #endregion
    }
}