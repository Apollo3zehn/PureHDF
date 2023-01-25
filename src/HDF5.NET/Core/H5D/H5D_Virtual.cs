namespace HDF5.NET
{
    internal class H5D_Virtual : H5D_Base
    {
        #region Constructors

        public H5D_Virtual(H5Dataset dataset, H5DatasetAccess datasetAccess) : 
            base(dataset, supportsBuffer: true, supportsStream: false, datasetAccess)
        {
            //
            var layoutMessage = (DataLayoutMessage4)dataset.InternalDataLayout;
            var collection = H5Cache.GetGlobalHeapObject(dataset.Context, layoutMessage.Address);
            var index = ((VirtualStoragePropertyDescription)layoutMessage.Properties).Index;
            var objectData = collection.GlobalHeapObjects[(int)index - 1].ObjectData;
            using var localReader = new H5StreamReader(new MemoryStream(objectData), leaveOpen: false);

            var vdsGlobalHeapBlock = new VdsGlobalHeapBlock(localReader, dataset.Context.Superblock);           
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
            throw new NotImplementedException();
        }

        #endregion
    }
}
