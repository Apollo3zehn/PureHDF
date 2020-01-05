using System.Collections.Generic;

namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription4 : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription4()
        {
            //
        }

        #endregion

        #region Properties

        public ChunkedStoragePropertyFlags Flags { get; set; }
        public byte Dimensionality { get; set; }
        public byte DimensionSizeEncodedLength { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public ChunkIndexingType ChunkIndexingType { get; set; }
        public IndexingInformation IndexingTypeInformation { get; set; }
        public ulong Address { get; set; }

        #endregion
    }
}
