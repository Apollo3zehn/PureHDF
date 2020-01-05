using System.Collections.Generic;

namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public ulong Address { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public uint DatasetElementSize { get; set; }

        #endregion
    }
}
