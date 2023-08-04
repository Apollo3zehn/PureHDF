namespace PureHDF;

internal class H5D_Contiguous : H5D_Base
{
    #region Fields

    private IH5ReadStream? _readStream;
    private IH5WriteStream? _writeStream;

    #endregion

    #region Constructors

    public H5D_Contiguous(NativeReadContext readContext, NativeWriteContext writeContext, DatasetInfo dataset, H5DatasetAccess datasetAccess) :
        base(readContext, writeContext, dataset, datasetAccess)
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

        if (_readStream is null)
        {
            if (ReadContext.Superblock.IsUndefinedAddress(address))
            {
                if (Dataset.ExternalFileList is not null)
                    _readStream = new ExternalFileListStream(ReadContext.File, Dataset.ExternalFileList, DatasetAccess);

                else
                    _readStream = new UnsafeFillValueStream(Dataset.FillValue.Value ?? new byte[] { 0 });
            }

            else
            {
                ReadContext.Driver.Seek((long)address, SeekOrigin.Begin);

                _readStream = new OffsetStream(ReadContext.Driver);
            }
        }

        return Task.FromResult(_readStream);
    }

    public override Task<IH5WriteStream> GetWriteStreamAsync<TReader>(TReader reader, ulong[] chunkIndices)
    {
        if (Dataset.Layout is DataLayoutMessage4 layout)
        {
            if (_writeStream is null)
            {
                WriteContext.Driver.Seek((long)layout.Address, SeekOrigin.Begin);
                _writeStream = new OffsetStream(WriteContext.Driver);
            }
        }

        else
        {
            throw new Exception("Only data layout message version 4 is supported.");
        }

        return Task.FromResult(_writeStream);
    }

    #endregion

    #region IDisposable

    protected override void Dispose(bool disposing)
    {
        _readStream?.Dispose();
        base.Dispose(disposing);
    }

    #endregion
}