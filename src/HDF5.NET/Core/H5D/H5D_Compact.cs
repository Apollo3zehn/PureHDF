using System.Runtime.InteropServices;

namespace HDF5.NET
{
    internal class H5D_Compact : H5D_Base
    {
        #region Constructors

        public H5D_Compact(H5Dataset dataset, H5DatasetAccess datasetAccess) : 
            base(dataset, supportsBuffer: true, supportsStream: false, datasetAccess)
        {
            //
        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.InternalDataspace.DimensionSizes;
        }

        public override Task<Memory<byte>> GetBufferAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            byte[] buffer;

            if (Dataset.InternalDataLayout is DataLayoutMessage12 layout12)
            {
// TODO: untested
                buffer = layout12.CompactData;
            }
            else if (Dataset.InternalDataLayout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                buffer = compact.RawData;
            }
            else
            {
                throw new Exception($"Data layout message type '{Dataset.InternalDataLayout.GetType().Name}' is not supported.");
            }

            return Task.FromResult(buffer.AsMemory());
        }

        public override H5Stream? GetH5Stream(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
