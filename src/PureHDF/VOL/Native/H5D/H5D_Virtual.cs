namespace PureHDF.VOL.Native;

internal class H5D_Virtual<TResult> : H5D_Base
{
    #region Fields

    private readonly VdsGlobalHeapBlock _block;
    private readonly TResult? _fillValue;
    private readonly ReadVirtualDelegate<TResult> _readVirtualDelegate;

    // The stream owns the cache of opened source files (one per VdsDatasetEntry).
    // We create it lazily on first GetReadStream call and tie its lifetime to
    // this instance, so the source-file handles are released when
    // we're disposed.
    private VirtualDatasetStream<TResult>? _stream;

    #endregion

    #region Constructors

    private H5D_Virtual(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        H5DatasetAccess datasetAccess,
        TResult? fillValue,
        ReadVirtualDelegate<TResult> readVirtualDelegate,
        VdsGlobalHeapBlock block)
        : base(readContext, writeContext, dataset, datasetAccess)
    {
        _fillValue = fillValue;
        _readVirtualDelegate = readVirtualDelegate;
        _block = block;

        // https://docs.hdfgroup.org/archive/support/HDF5/docNewFeatures/VDS/HDF5-VDS-requirements-use-cases-2014-12-10.pdf
        // "A source dataset may have different rank and dimension sizes than the VDS. However, if a
        // source dataset has an unlimited dimension, it must be the slowest-­changing dimension, and
        // the virtual dataset must be the same rank and have the same dimension as unlimited."

        // -> for now unlimited dimensions will not be supported

        foreach (var dimension in Dataset.Space.Dimensions)
        {
            if (dimension == H5Constants.Unlimited)
                throw new Exception("Virtual datasets with unlimited dimensions are not supported.");
        }
    }

    // RULE 4 CONVERSION: the constructor performed a driver read (VdsGlobalHeapBlock.Decode)
    // and constructors cannot be async, so construction is now via this static factory.
    public static async ValueTask<H5D_Virtual<TResult>> Create(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        H5DatasetAccess datasetAccess,
        TResult? fillValue,
        ReadVirtualDelegate<TResult> readVirtualDelegate)
    {
        var layoutMessage = (DataLayoutMessage4)dataset.Layout;
        var collection = await NativeCache
            .GetGlobalHeapObject(readContext, ((VirtualStoragePropertyDescription)layoutMessage.Properties).Address)
            .ConfigureAwait(false);
        var index = ((VirtualStoragePropertyDescription)layoutMessage.Properties).Index;
        var objectData = collection.GlobalHeapObjects[(int)index].ObjectData;
        using var localDriver = new H5StreamDriver(new MemoryStream(objectData), leaveOpen: false);

        var block = await VdsGlobalHeapBlock.Decode(localDriver, readContext.Superblock).ConfigureAwait(false);

        return new H5D_Virtual<TResult>(
            readContext,
            writeContext,
            dataset,
            datasetAccess,
            fillValue,
            readVirtualDelegate,
            block);
    }

    #endregion

    #region Properties

    #endregion

    #region Methods

    public override ulong[] GetChunkDims()
    {
        return Dataset.Space.Dimensions;
    }

    public override IH5ReadStream GetReadStream(ulong chunkIndex)
    {
        _stream ??= new VirtualDatasetStream<TResult>(
            ReadContext.File,
            _block.VdsDatasetEntries,
            dimensions: Dataset.Space.Dimensions,
            fillValue: _fillValue,
            DatasetAccess,
            _readVirtualDelegate
        );

        return _stream;
    }

    public override IH5WriteStream GetWriteStream(ulong chunkIndex)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    #endregion
}