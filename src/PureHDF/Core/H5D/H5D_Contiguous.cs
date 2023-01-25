﻿namespace PureHDF
{
    internal class H5D_Contiguous : H5D_Base
    {
        #region Fields

        private Stream? _stream;

        #endregion

        #region Constructors

        public H5D_Contiguous(H5Dataset dataset, H5DatasetAccess datasetAccess) :
            base(dataset, supportsBuffer: false, supportsStream: true, datasetAccess)
        {
            //
        }

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.GetDatasetDims();
        }

        public override Task<Memory<byte>> GetBufferAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        public override Stream? GetH5Stream(ulong[] chunkIndices)
        {
            var address = Dataset.InternalDataLayout.Address;

            if (_stream is null)
            {
                if (Dataset.Context.Superblock.IsUndefinedAddress(address))
                {
                    if (Dataset.InternalExternalFileList is not null)
                        _stream = new ExternalFileListStream(Dataset.InternalExternalFileList, DatasetAccess);

                    else if (Dataset.InternalFillValue.Value is not null)
                        _stream = new UnsafeFillValueStream(Dataset.InternalFillValue.Value);

                    else
                        _stream = null;
                }
                else
                {
                    Dataset.Context.Reader.Seek((long)address, SeekOrigin.Begin);

                    _stream = new OffsetStream(Dataset.Context.Reader);
                }
            }

            return _stream;
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
