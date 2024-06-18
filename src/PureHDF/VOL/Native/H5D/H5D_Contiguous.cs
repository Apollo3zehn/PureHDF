namespace PureHDF.VOL.Native;

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
        return Dataset.Space.GetDims();
    }

    public override IH5ReadStream GetReadStream(ulong chunkIndex)
    {
        if (_readStream is null)
        {
            var address = GetAddress();

            if (ReadContext.Superblock.IsUndefinedAddress(address))
            {
                if (Dataset.ExternalFileList is not null)
                    _readStream = new ExternalFileListStream(ReadContext.File, Dataset.ExternalFileList, DatasetAccess);

                else
                    _readStream = new UnsafeFillValueStream(Dataset.FillValue.Value ?? [0]);
            }

            else
            {
                ReadContext.Driver.Seek((long)address, SeekOrigin.Begin);

                _readStream = new OffsetStream(ReadContext.Driver);
            }
        }

        return _readStream;
    }

    public override IH5WriteStream GetWriteStream(ulong chunkIndex)
    {
        if (Dataset.Layout is DataLayoutMessage4)
        {
            if (_writeStream is null)
            {
                WriteContext.Driver.Seek((long)GetAddress(), SeekOrigin.Begin);
                _writeStream = new OffsetStream(WriteContext.Driver);
            }
        }

        else
        {
            throw new Exception("Only data layout message version 4 is supported.");
        }

        return _writeStream;
    }

    private ulong GetAddress()
    {
        return Dataset.Layout switch
        {
            DataLayoutMessage12 layout12 => layout12.Address,
            DataLayoutMessage3 layout3 => ((ContiguousStoragePropertyDescription)layout3.Properties).Address,
            _ => throw new Exception($"The layout {Dataset.Layout} is not supported.")
        };
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