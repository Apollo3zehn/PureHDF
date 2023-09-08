namespace PureHDF.VOL.Native;

internal abstract class H5D_Base : IDisposable
{
    public H5D_Base(
        NativeReadContext readContext, 
        NativeWriteContext writeContext, 
        DatasetInfo dataset,
        H5DatasetAccess datasetAccess)
    {
        ReadContext = readContext;
        WriteContext = writeContext;
        Dataset = dataset;
        DatasetAccess = datasetAccess;
    }

    public DatasetInfo Dataset { get; }

    protected NativeReadContext ReadContext { get; }

    protected NativeWriteContext WriteContext { get; }

    protected H5DatasetAccess DatasetAccess { get; }

    public virtual void Initialize()
    {
        //
    }

    public abstract ulong[] GetChunkDims();

    public abstract IH5ReadStream GetReadStream(ulong[] chunkIndices);

    public abstract IH5WriteStream GetWriteStream(ulong[] chunkIndices);

    protected virtual void Dispose(bool disposing)
    {
        //
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}