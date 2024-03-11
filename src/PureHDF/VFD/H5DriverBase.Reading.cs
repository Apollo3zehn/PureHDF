namespace PureHDF.VFD;

internal abstract partial class H5DriverBase : IH5ReadStream
{
    private ulong _baseAddress;

    public ulong BaseAddress { get => _baseAddress; }

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

    public void SetBaseAddress(ulong baseAddress)
    {
        _baseAddress = baseAddress;
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