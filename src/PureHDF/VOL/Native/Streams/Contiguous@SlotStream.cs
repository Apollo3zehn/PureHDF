namespace PureHDF;

internal class SlotStream : IH5ReadStream
{
    private long _position;
    private readonly NativeFile _file;
    private readonly string _name;
    private Stream? _stream;
    private readonly ExternalFileListSlot _slot;
    private readonly H5DatasetAccess _datasetAccess;

    private SlotStream(NativeFile file, string name, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
    {
        _file = file;
        _name = name;
        _slot = slot;
        Offset = offset;
        _datasetAccess = datasetAccess;

        Length = (long)_slot.Size;
    }

    // RULE 4 CONVERSION: the constructor resolved the external-file name via
    // LocalHeap.GetObjectName, which performs a driver read; constructors cannot be
    // async, so construction is now via this static factory.
    public static async ValueTask<SlotStream> Create(NativeFile file, LocalHeap heap, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
    {
        var name = await heap.GetObjectName(slot.NameHeapOffset).ConfigureAwait(false);

        return new SlotStream(file, name, slot, offset, datasetAccess);
    }

    public long Offset { get; private set; }

    public long Position { get => _position; }

    public long Length { get; }

    public async ValueTask ReadDataset(Memory<byte> buffer)
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

