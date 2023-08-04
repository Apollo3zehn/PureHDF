namespace PureHDF
{
    internal class H5D_Compact : H5D_Base
    {
        #region Constructors

        public H5D_Compact(NativeContext context, DatasetInfo dataset, H5DatasetAccess datasetAccess) :
            base(context, dataset, datasetAccess)
        {
            //
        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.Space.DimensionSizes;
        }

        public override Task<IH5ReadStream> GetReadStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
        {
            byte[] buffer;

            if (Dataset.Layout is DataLayoutMessage12 layout12)
            {
                // TODO: untested
                buffer = layout12.CompactData;
            }

            else if (Dataset.Layout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                buffer = compact.InputData;
            }
            
            else
            {
                throw new Exception($"Data layout message type '{Dataset.Layout.GetType().Name}' is not supported.");
            }

            IH5ReadStream stream = new SystemMemoryStream(buffer);

            return Task.FromResult(stream);
        }

        #endregion
    }
}
