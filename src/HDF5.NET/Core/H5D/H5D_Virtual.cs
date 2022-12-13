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
            var collection = H5Cache.GetGlobalHeapObject(dataset.Context.Reader, dataset.Context.Superblock, layoutMessage.Address);
            var index = ((VirtualStoragePropertyDescription)layoutMessage.Properties).Index;
            var objectData = collection.GlobalHeapObjects[(int)index - 1].ObjectData;
            using var localReader = new H5BinaryReader(new MemoryStream(objectData));

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

        public override Memory<byte> GetBuffer(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        public override Stream? GetStream(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
