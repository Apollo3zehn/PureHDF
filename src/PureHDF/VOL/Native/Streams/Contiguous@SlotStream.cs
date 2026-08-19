namespace PureHDF;

internal class SlotStream : IH5ReadStream
{
    private long _position;
    private readonly NativeFile _file;
    private readonly string _name;
    private Stream? _stream;
    private readonly ExternalFileListSlot _slot;
    private readonly H5DatasetAccess _datasetAccess;

    // This briefly needed a static async factory instead, because resolving the name through the
    // local heap performed a driver read and a constructor cannot be async. LocalHeap now holds its
    // data segment outright, so the lookup is a synchronous array scan and a constructor does again.
    public SlotStream(NativeFile file, LocalHeap heap, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
    {
        _file = file;
        _name = heap.GetObjectName(slot.NameHeapOffset);
        _slot = slot;
        Offset = offset;
        _datasetAccess = datasetAccess;

        Length = (long)_slot.Size;
    }

    public long Offset { get; private set; }

    public long Position { get => _position; }

    public long Length { get; }

    public async ValueTask ReadDatasetAsync(Memory<byte> buffer)
    {
        var length = (int)Math.Min(Length - Position, buffer.Length);

        _stream = EnsureStream();

        var actualLength = await _stream.ReadAsync(buffer[..length]).ConfigureAwait(false);

        // If file is shorter than slot: fill remaining buffer with zeros.
        buffer.Span[actualLength..length]
            .Clear();

        _position += length;
    }

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

    private Stream EnsureStream()
    {
        if (_stream is null)
        {
            var filePath = FilePathUtils.FindExternalFileForDatasetAccess(_file.FolderPath, _name, _datasetAccess);

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

