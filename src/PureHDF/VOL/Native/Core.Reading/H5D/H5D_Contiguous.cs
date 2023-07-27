namespace PureHDF
{
    internal class H5D_Contiguous : H5D_Base
    {
        #region Fields

        private IH5ReadStream? _stream;

        #endregion

        #region Constructors

        public H5D_Contiguous(NativeDataset dataset, H5DatasetAccess datasetAccess) :
            base(dataset, datasetAccess)
        {
            //
        }

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.GetDatasetDims();
        }

        public override Task<IH5ReadStream> GetStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            var address = Dataset.DataLayoutMessage.Address;

            if (_stream is null)
            {
                if (Dataset.Context.Superblock.IsUndefinedAddress(address))
                {
                    if (Dataset.InternalExternalFileList is not null)
                        _stream = new ExternalFileListStream((H5File)Dataset.File, Dataset.InternalExternalFileList, DatasetAccess);

                    else
                        _stream = new UnsafeFillValueStream(Dataset.FillValueMessage.Value ?? new byte[] { 0 });
                }
                else
                {
                    Dataset.Context.Driver.Seek((long)address, SeekOrigin.Begin);

                    _stream = new OffsetStream(Dataset.Context.Driver);
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
