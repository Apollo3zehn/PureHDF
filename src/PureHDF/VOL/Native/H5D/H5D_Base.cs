namespace PureHDF
{
    internal abstract class H5D_Base : IDisposable
    {
        public H5D_Base(NativeContext context, DatasetInfo dataset, H5DatasetAccess datasetAccess)
        {
            Context = context;
            Dataset = dataset;
            DatasetAccess = datasetAccess;
        }

        public NativeContext Context { get; }

        public DatasetInfo Dataset { get; }

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
