namespace PureHDF;
internal class OffsetStream : IH5ReadStream
{
    private readonly H5BaseReader _reader;
    private readonly long _baseAddress;

    public OffsetStream(H5BaseReader reader)
    {
        _reader = reader;
        _baseAddress = reader.Position;
    }

    public long Position { get => _reader.Position - _baseAddress; }

    public void ReadDataset(Memory<byte> buffer)
    {
        _reader.ReadDataset(buffer);
    }

#if NET6_0_OR_GREATER
    public ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadDatasetAsync(buffer, cancellationToken);
    }
#endif

    public void Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _reader.Seek(_baseAddress + offset, SeekOrigin.Begin);
                break;

            case SeekOrigin.Current:
                _reader.Seek(offset, SeekOrigin.Current);
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