namespace PureHDF.VFD;

internal abstract partial class H5DriverBase : IH5ReadStream
{
    public ulong BaseAddress { get; private set; }

    public abstract long Position { get; }
    public abstract long Length { get; }

    public abstract void ReadDataset(Span<byte> buffer);
    public abstract void Seek(long offset, SeekOrigin origin);

    public abstract void Read(Span<byte> buffer);
    public abstract byte ReadByte();
    public abstract byte[] ReadBytes(int count);
    public abstract ushort ReadUInt16();
    public abstract short ReadInt16();
    public abstract uint ReadUInt32();
    public abstract ulong ReadUInt64();

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