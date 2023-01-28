namespace PureHDF
{
    internal class H5D_Virtual : H5D_Base
    {
        #region Fields

        private readonly VdsGlobalHeapBlock _block;
        private readonly int _typeSize;

        #endregion

        #region Constructors

        public H5D_Virtual(H5Dataset dataset, H5DatasetAccess datasetAccess) :
            base(dataset, supportsBuffer: false, supportsStream: true, datasetAccess)
        {
            var layoutMessage = (DataLayoutMessage4)dataset.InternalDataLayout;
            var collection = H5Cache.GetGlobalHeapObject(dataset.Context, layoutMessage.Address);
            var index = ((VirtualStoragePropertyDescription)layoutMessage.Properties).Index;
            var objectData = collection.GlobalHeapObjects[(int)index - 1].ObjectData;
            using var localReader = new H5StreamReader(new MemoryStream(objectData), leaveOpen: false);

            _block = new VdsGlobalHeapBlock(localReader, dataset.Context.Superblock);
            _typeSize = (int)dataset.InternalDataType.Size;
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

        public override Stream? GetH5Stream(ulong[] chunkIndices)
        {
            return new VirtualDatasetStream(_block.VdsDatasetEntries, _typeSize);
        }

        #endregion
    }
}
