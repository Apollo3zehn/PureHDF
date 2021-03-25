using System;
using System.IO;

namespace HDF5.NET
{
    internal abstract class H5D_Base : IDisposable
    {
        #region Constructors

        public H5D_Base(H5Dataset dataset, bool supportsBuffer, bool supportsStream, H5DatasetAccess datasetAccess)
        {
            this.Dataset = dataset;
            this.SupportsBuffer = supportsBuffer;
            this.SupportsStream = supportsStream;
            this.DatasetAccess = datasetAccess;
        }

        #endregion

        #region Properties

        public H5Dataset Dataset { get; }

        public bool SupportsBuffer { get; }

        public bool SupportsStream { get; }

        public H5DatasetAccess DatasetAccess { get; }

        #endregion

        #region Methods

        public ulong[] GetDatasetDims()
        {
            return this.Dataset.InternalDataspace.Type switch
            {
                DataspaceType.Scalar    => new ulong[] { 1 },
                DataspaceType.Simple    => this.Dataset.InternalDataspace.DimensionSizes,
                _                       => throw new Exception($"Unsupported data space type '{this.Dataset.InternalDataspace.Type}'.")
            };
        }

        public HyperslabSelection GetSelection()
        {
            return this.Dataset.InternalDataspace.Type switch
            {
                DataspaceType.Scalar => HyperslabSelection.Scalar(),
                DataspaceType.Simple => HyperslabSelection.All(this.GetDatasetDims()),
                _ => throw new Exception($"Unsupported data space type '{this.Dataset.InternalDataspace.Type}'.")
            };
        }

        public virtual void Initialize()
        {
            //
        }

        public abstract ulong[] GetChunkDims();

        public abstract Memory<byte> GetBuffer(ulong[] chunkIndices);

        public abstract Stream? GetStream(ulong[] chunkIndices);

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            //
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
