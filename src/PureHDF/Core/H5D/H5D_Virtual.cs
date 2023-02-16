namespace PureHDF
{
    internal class H5D_Virtual : H5D_Base
    {
        #region Fields

        private readonly VdsGlobalHeapBlock _block;

        #endregion

        #region Constructors

        public H5D_Virtual(H5Dataset dataset, H5DatasetAccess datasetAccess) :
            base(dataset, supportsBuffer: false, supportsStream: true, datasetAccess)
        {
            var layoutMessage = (DataLayoutMessage4)dataset.InternalDataLayout;
            var collection = H5Cache.GetGlobalHeapObject(dataset.Context, layoutMessage.Address);
            var index = ((VirtualStoragePropertyDescription)layoutMessage.Properties).Index;
            var objectData = collection.GlobalHeapObjects[(int)index].ObjectData;
            using var localReader = new H5StreamReader(new MemoryStream(objectData), leaveOpen: false);

            _block = new VdsGlobalHeapBlock(localReader, dataset.Context.Superblock);

            // https://docs.hdfgroup.org/archive/support/HDF5/docNewFeatures/VDS/HDF5-VDS-requirements-use-cases-2014-12-10.pdf
            // "A source dataset may have different rank and dimension sizes than the VDS. However, if a
            // source dataset has an unlimited dimension, it must be the slowest-­changing dimension, and
            // the virtual dataset must be the same rank and have the same dimension as unlimited."

            // -> for now unlimited dimensions will not be supported

            foreach (var dimension in Dataset.InternalDataspace.DimensionSizes)
            {
                if (dimension == H5Constants.Unlimited)
                    throw new Exception("Virtual datasets with unlimited dimensions are not supported.");
            }
        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.InternalDataspace.DimensionSizes;
        }

        public override Task<Memory<byte>> GetBufferAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        public override Stream GetH5Stream(ulong[] chunkIndices)
        {
            return new VirtualDatasetStream(
                Dataset.File,
                _block.VdsDatasetEntries, 
                dimensions: Dataset.InternalDataspace.DimensionSizes,
                typeSize: Dataset.InternalDataType.Size,
                fillValue: Dataset.InternalFillValue.Value,
                DatasetAccess);
        }

        #endregion
    }
}
