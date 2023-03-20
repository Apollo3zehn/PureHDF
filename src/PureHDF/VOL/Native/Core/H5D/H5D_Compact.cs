namespace PureHDF
{
    internal class H5D_Compact : H5D_Base
    {
        #region Constructors

        public H5D_Compact(H5Dataset dataset, H5DatasetAccess datasetAccess) :
            base(dataset, datasetAccess)
        {
            //
        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.DataspaceMessage.DimensionSizes;
        }

        public override Task<IH5ReadStream> GetStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            byte[] buffer;

            if (Dataset.DataLayoutMessage is DataLayoutMessage12 layout12)
            {
                // TODO: untested
                buffer = layout12.CompactData;
            }
            else if (Dataset.DataLayoutMessage is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                buffer = compact.RawData;
            }
            else
            {
                throw new Exception($"Data layout message type '{Dataset.DataLayoutMessage.GetType().Name}' is not supported.");
            }

            IH5ReadStream stream = new SystemMemoryStream(buffer);

            return Task.FromResult(stream);
        }

        #endregion
    }
}
