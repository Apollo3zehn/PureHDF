namespace PureHDF.VFD;

internal abstract partial class H5DriverBase : IH5ReadStream
{
    public ulong BaseAddress { get; private set; }

    public abstract long Position { get; }
    public abstract long Length { get; }

    public abstract ValueTask ReadDataset(Memory<byte> buffer);

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
