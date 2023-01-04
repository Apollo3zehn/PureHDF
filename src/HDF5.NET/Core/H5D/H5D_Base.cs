namespace HDF5.NET
{
    internal abstract class H5D_Base : IDisposable
    {
        #region Constructors

        public H5D_Base(H5Dataset dataset, bool supportsBuffer, bool supportsStream, H5DatasetAccess datasetAccess)
        {
            Dataset = dataset;
            SupportsBuffer = supportsBuffer;
            SupportsStream = supportsStream;
            DatasetAccess = datasetAccess;
        }

        #endregion

        #region Properties

        public H5Dataset Dataset { get; }

        public bool SupportsBuffer { get; }

        public bool SupportsStream { get; }

        public H5DatasetAccess DatasetAccess { get; }

        #endregion

        #region Methods

        public virtual void Initialize()
        {
            //
        }

        public abstract ulong[] GetChunkDims();

        public abstract Task<Memory<byte>> GetBufferAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader;

        public abstract Stream? GetStream(ulong[] chunkIndices);

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            //
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
