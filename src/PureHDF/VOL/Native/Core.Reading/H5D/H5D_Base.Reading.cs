namespace PureHDF
{
    internal abstract partial class H5D_Base : IDisposable
    {
        public H5D_Base(NativeDataset dataset, H5DatasetAccess datasetAccess)
        {
            Dataset = dataset;
            DatasetAccess = datasetAccess;
        }

        public NativeDataset Dataset { get; }

        public H5DatasetAccess DatasetAccess { get; }

        public virtual void Initialize()
        {
            //
        }

        public abstract ulong[] GetChunkDims();

        public abstract Task<IH5ReadStream> GetReadStreamAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader;

        protected virtual void Dispose(bool disposing)
        {
            //
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
