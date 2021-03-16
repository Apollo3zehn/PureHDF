using System.Linq;

namespace HDF5.NET
{
    internal abstract class H5D_Chunk4 : H5D_Chunk
    {
        #region Constructors

        public H5D_Chunk4(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) : 
            base(dataset, datasetAccess)
        {
            this.Layout = layout;
            this.Chunked4 = (ChunkedStoragePropertyDescription4)layout.Properties;
        }

        #endregion

        #region Properties

        public DataLayoutMessage4 Layout { get; }

        public ChunkedStoragePropertyDescription4 Chunked4 { get; }

        #endregion

        #region Methods

        protected override ulong[] GetRawChunkDims()
        {
            return this.Chunked4
                .DimensionSizes
                .ToArray();
        }

        #endregion
    }
}
