namespace PureHDF;

internal class ExternalFileListStream : IH5ReadStream
{
    private readonly long _length;
    private long _position;
    private bool _loadSlot;
    private SlotStream? _slotStream;
    private readonly SlotStream[] _slotStreams;

    public ExternalFileListStream(
        NativeFile file,
        ExternalFileListMessage externalFileList,
        H5DatasetAccess datasetAccess)
    {
        var offset = 0L;

        _slotStreams = externalFileList
            .SlotDefinitions
            .Select(slot =>
            {
                var stream = new SlotStream(file, externalFileList.Heap, slot, offset, datasetAccess);
                offset += stream.Length;
                return stream;
            })
            .ToArray();

        if (_slotStreams.Any())
        {
            _length =
                _slotStreams.Last().Offset +
                _slotStreams.Last().Length;
        }
        else
        {
            throw new Exception("There must at least a single file be defined in the external file list.");
        }
    }

    public long Position { get => _position; }

    public void ReadDataset(Memory<byte> buffer)
    {
        var offset = 0;
        var remaining = buffer.Length;

        while (remaining > 0)
        {
            if (_slotStream is null || _loadSlot)
            {
                _slotStream = _slotStreams.Last(stream => stream.Offset <= _position);
                _slotStream.Seek(_position - _slotStream.Offset, SeekOrigin.Begin);
                _loadSlot = false;
            }

            var streamRemaining = _slotStream.Length - _slotStream.Position;

            if (streamRemaining <= 0)
                throw new Exception("The current stream has already been consumed.");

            var length = (int)Math.Min(remaining, streamRemaining);

            _slotStream.ReadDataset(buffer.Slice(offset, length));
            _position += length;
            offset += length;
            remaining -= length;

            if (length == streamRemaining)
                _loadSlot = true;
        }
    }
    public void Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:

                if (offset > _length)
                    throw new Exception("The offset exceeds the stream length.");

                if (_slotStream is null)
                {
                    _loadSlot = true;
                }
                else
                {
                    var isInRange = _slotStream.Offset <= offset && offset < _slotStream.Length;

                    if (!isInRange)
                        _loadSlot = true;

                    else
                        _slotStream.Seek(offset - _slotStream.Offset, origin);
                }

                _position = offset;

                break;

            default:
                throw new NotImplementedException();
        }
    }

    #region IDisposable

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            foreach (var stream in _slotStreams)
            {
                stream.Dispose();
            }

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
