namespace PureHDF
{
    internal class H5D_Contiguous : H5D_Base
    {
        #region Fields

        private IH5ReadStream? _stream;

        #endregion

        #region Constructors

        public H5D_Contiguous(NativeContext context, DatasetInfo dataset, H5DatasetAccess datasetAccess) :
            base(context, dataset, datasetAccess)
        {
            //
        }

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.GetDatasetDims();
        }

        public override Task<IH5ReadStream> GetReadStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            var address = Dataset.Layout.Address;

            if (_stream is null)
            {
                if (Context.Superblock.IsUndefinedAddress(address))
                {
                    if (Dataset.ExternalFileList is not null)
                        _stream = new ExternalFileListStream(Context.File, Dataset.ExternalFileList, DatasetAccess);

                    else
                        _stream = new UnsafeFillValueStream(Dataset.FillValue.Value ?? new byte[] { 0 });
                }
                else
                {
                    Context.Driver.Seek((long)address, SeekOrigin.Begin);

                    _stream = new OffsetStream(Context.Driver);
                }
            }

            return Task.FromResult(_stream);
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            _stream?.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
