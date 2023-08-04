namespace PureHDF
{
    internal abstract class H5D_Chunk4 : H5D_Chunk
    {
        #region Constructors

        public H5D_Chunk4(NativeContext context, DatasetInfo dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess)
            : base(context, dataset, datasetAccess)
        {
            Layout = layout;
            Chunked4 = (ChunkedStoragePropertyDescription4)layout.Properties;
        }

        #endregion

        #region Properties

        public DataLayoutMessage4 Layout { get; }

        public ChunkedStoragePropertyDescription4 Chunked4 { get; }

        #endregion

        #region Methods

        protected override ulong[] GetRawChunkDims()
        {
            return Chunked4
                .DimensionSizes
                .ToArray();
        }

        #endregion
    }
}
