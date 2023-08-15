namespace PureHDF;

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

    public NativeReadContext ReadContext { get; }

    public NativeWriteContext WriteContext { get; }

    public DatasetInfo Dataset { get; }

    public H5DatasetAccess DatasetAccess { get; }

    public virtual void Initialize()
    {
        //
    }

    public abstract ulong[] GetChunkDims();

    public abstract Task<IH5ReadStream> GetReadStreamAsync<TReader>(TReader reader, ulong[] chunkIndices) 
        where TReader : IReader;

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