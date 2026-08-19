namespace PureHDF.VFD;

internal abstract partial class H5DriverBase : IH5ReadStream
{
    public ulong BaseAddress { get; private set; }

    public abstract long Position { get; }
    public abstract long Length { get; }

    public abstract ValueTask ReadDatasetAsync(Memory<byte> buffer);

    // Defaults to "cannot read synchronously" so that a driver over a genuinely remote source is
    // never accidentally blocked on. Drivers over local sources override it; see IH5ReadStream.
    public virtual bool TryReadDatasetSync(Span<byte> buffer) => false;

    // Pure arithmetic — deliberately left synchronous.
    public abstract void Seek(long offset, SeekOrigin origin);

    public abstract ValueTask Read(Memory<byte> buffer);
    public abstract ValueTask<byte> ReadByte();
    public abstract ValueTask<byte[]> ReadBytes(int count);
    public abstract ValueTask<ushort> ReadUInt16();
    public abstract ValueTask<short> ReadInt16();
    public abstract ValueTask<uint> ReadUInt32();
    public abstract ValueTask<ulong> ReadUInt64();

    public void SeekRelativeToBaseAddress(long offset)
    {
        Seek((long)BaseAddress + offset, SeekOrigin.Begin);
    }

    public void SetBaseAddress(ulong baseAddress)
    {
        BaseAddress = baseAddress;
    }

    // CONCURRENCY: returns a driver over the SAME underlying source but with its own independent
    // cursor, so one resolved dataset/attribute can be read from several threads at once. `null`
    // means "this source cannot be read concurrently; reuse me" - a plain Stream, for instance, has
    // exactly one cursor and no positionless read API. (A Stream that implements IConcurrentStream
    // does offer positionless reads and therefore does isolate; see H5StreamDriver.)
    //
    // Deliberately NOT virtual. BaseAddress is mutable driver state, set once by NativeFile after
    // the superblock is decoded, and every SeekRelativeToBaseAddress depends on it; a
    // per-operation driver that forgot to inherit it would read at wrong offsets throughout.
    // Copying it here rather than in each override means an implementer cannot forget it.
    // Override CreateOperationDriverCore instead.
    public H5DriverBase? TryCreateOperationDriver()
    {
        var operationDriver = CreateOperationDriverCore();

        if (operationDriver is null)
            return null;

        operationDriver.SetBaseAddress(BaseAddress);

        return operationDriver;
    }

    // Implementations must return a driver that shares this driver's source and does NOT own it:
    // disposing the returned driver has to leave the shared handle / accessor open.
    //
    // The returned driver's cursor deliberately starts at 0 instead of inheriting Position. Every
    // read path seeks before its first read (H5D_Contiguous, every chunk index, and
    // NativeCache.GetGlobalHeapObject all call SeekRelativeToBaseAddress first), so inheriting
    // buys nothing - and it would mean reading a field that concurrent operations mutate.
    protected virtual H5DriverBase? CreateOperationDriverCore() => null;

    #region IDisposable

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
            _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    #endregion
}
