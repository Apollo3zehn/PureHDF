using System;
using System.IO;

namespace HDF5.NET
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
            return this.GetDatasetDims();
        }

        public override Memory<byte> GetBuffer(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        public override Stream? GetStream(ulong[] chunkIndices)
        {
            var address = this.Dataset.InternalDataLayout.Address;

            if (_stream is null)
            {
                if (this.Dataset.Context.Superblock.IsUndefinedAddress(address))
                {
                    if (this.Dataset.InternalExternalFileList is not null)
                        _stream = new ExternalFileListStream(this.Dataset.InternalExternalFileList, this.DatasetAccess);

                    else if (this.Dataset.InternalFillValue.IsDefined)
                        _stream = new UnsafeFillValueStream(this.Dataset.InternalFillValue.Value);

                    else
                        _stream = null;
                }
                else
                {
                    this.Dataset.Context.Reader.Seek((long)address, SeekOrigin.Begin);
                    _stream = new OffsetStream(this.Dataset.Context.Reader.BaseStream, this.Dataset.Context.Reader.BaseStream.Position);
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
