namespace PureHDF;

internal partial class OffsetStream : IH5ReadStream
{
    private readonly H5DriverBase _driver;

    private readonly long _baseAddress;

    public OffsetStream(H5DriverBase driver)
    {
        _driver = driver;
        _baseAddress = driver.Position;
    }

    public long Position { get => _driver.Position - _baseAddress; }

    public void ReadDataset(Span<byte> buffer)
    {
        _driver.ReadDataset(buffer);
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _driver.Seek(_baseAddress + offset, SeekOrigin.Begin);
                break;

            case SeekOrigin.Current:
                _driver.Seek(offset, SeekOrigin.Current);
                break;

            default:
                throw new Exception($"Seek origin '{origin}' is not supported.");
        }
    }

    public void Dispose()
    {
        // do nothing
    }
}