namespace PureHDF
{
    internal abstract class H5D_Base : IDisposable
    {
        #region Constructors

        public H5D_Base(H5Dataset dataset, H5DatasetAccess datasetAccess)
        {
            Dataset = dataset;
            DatasetAccess = datasetAccess;
        }

        #endregion

        #region Properties

        public H5Dataset Dataset { get; }

        public H5DatasetAccess DatasetAccess { get; }

        #endregion

        #region Methods

        public virtual void Initialize()
        {
            //
        }

        public abstract ulong[] GetChunkDims();

        public abstract Task<IH5ReadStream> GetStreamAsync<TReader>(TReader reader, ulong[] chunkIndices) where TReader : IReader;

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
