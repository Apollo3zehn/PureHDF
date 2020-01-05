using System.Collections.Generic;

namespace HDF5.NET
{
    public class DataLayoutMessage12
    {
        #region Constructors

        public DataLayoutMessage12()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public byte Dimensionality { get; set; }
        public LayoutClass LayoutClass { get; set; }
        public ulong DataAddress { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public uint DatasetElementSize { get; set; }
        public uint CompactDataSize { get; set; }
        public byte[] CompactData { get; set; }

        #endregion
    }
}
