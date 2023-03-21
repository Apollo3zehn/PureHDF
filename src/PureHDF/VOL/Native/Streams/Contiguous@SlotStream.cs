namespace PureHDF;

internal class SlotStream : IH5ReadStream
{
    private long _position;
    private readonly NativeFile _file;
    private readonly LocalHeap _heap;
    private Stream? _stream;
    private readonly ExternalFileListSlot _slot;
    private readonly H5DatasetAccess _datasetAccess;

    public SlotStream(NativeFile file, LocalHeap heap, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
    {
        _file = file;
        _heap = heap;
        _slot = slot;
        Offset = offset;
        _datasetAccess = datasetAccess;

        Length = (long)_slot.Size;
    }

    public long Offset { get; private set; }

    public long Position { get => _position; }

    public long Length { get; }

    public void ReadDataset(Memory<byte> buffer)
    {
        var length = (int)Math.Min(Length - Position, buffer.Length);

        _stream = EnsureStream();

        var actualLength = _stream.Read(buffer.Span[..length]);

        // If file is shorter than slot: fill remaining buffer with zeros.
        buffer[actualLength..length]
            .Span
            .Clear();

        _position += length;
    }

#if NET6_0_OR_GREATER
    public async ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var length = (int)Math.Min(Length - Position, buffer.Length);

        _stream = EnsureStream();

        var actualLength = await _stream
            .ReadAsync(buffer[..length], cancellationToken)
            .ConfigureAwait(false);

        // If file is shorter than slot: fill remaining buffer with zeros.
        buffer
            .Span[actualLength..length]
            .Fill(0);

        _position += length;
    }
#endif

    public void Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:

                if (offset < 0 || offset > Length)
                    throw new Exception("The offset exceeds the stream length.");

                _stream = EnsureStream();
                _stream.Seek(offset + (long)_slot.Offset, origin);
                _position = offset;

                break;

            default:
                throw new NotImplementedException();
        }
    }

    // TODO File should be opened asynchronously if this file is also opened asynchronously.
    private Stream EnsureStream()
    {
        if (_stream is null)
        {
            var name = _heap.GetObjectName(_slot.NameHeapOffset);
            var filePath = FilePathUtils.FindExternalFileForDatasetAccess(_file.FolderPath, name, _datasetAccess);

            if (!File.Exists(filePath))
                throw new Exception($"External file '{filePath}' does not exist.");

            _stream = File.OpenRead(filePath!);
            _stream.Seek((long)_slot.Offset, SeekOrigin.Begin);
        }

        return _stream;
    }

#region IDisposable

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _stream?.Dispose();
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

#endregion

}

