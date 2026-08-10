namespace PureHDF;

internal class ExternalFileListStream : IH5ReadStream
{
    private readonly long _length;
    private long _position;
    private bool _loadSlot;
    private SlotStream? _slotStream;
    private readonly NativeFile _file;
    private readonly NativeReadContext _context;
    private readonly ExternalFileListMessage _externalFileList;
    private readonly H5DatasetAccess _datasetAccess;
    private readonly ExternalFileListSlot[] _slots;
    private readonly long[] _offsets;
    private readonly SlotStream?[] _slotStreamCache;

    // CONCURRENCY: `context` must be the context of the read operation that created this stream, not
    // the file-level one. The local heap holding the external file names is decoded through it, and
    // this stream lives exactly as long as the H5D_Contiguous that owns it - i.e. one operation.
    public ExternalFileListStream(
        NativeReadContext context,
        ExternalFileListMessage externalFileList,
        H5DatasetAccess datasetAccess)
    {
        _context = context;
        _file = context.File;
        _externalFileList = externalFileList;
        _datasetAccess = datasetAccess;

        _slots = externalFileList.SlotDefinitions;
        _offsets = new long[_slots.Length];
        _slotStreamCache = new SlotStream?[_slots.Length];

        var offset = 0L;

        for (var i = 0; i < _slots.Length; i++)
        {
            _offsets[i] = offset;
            offset += (long)_slots[i].Size;
        }

        if (_slots.Length > 0)
        {
            _length =
                _offsets[^1] +
                (long)_slots[^1].Size;
        }
        else
        {
            throw new Exception("There must at least a single file be defined in the external file list.");
        }
    }

    public long Position { get => _position; }

    private async ValueTask<SlotStream> GetOrCreateSlotStream(int index)
    {
        var cached = _slotStreamCache[index];

        if (cached is not null)
            return cached;

        var heap = await _externalFileList.Heap(_context).ConfigureAwait(false);

        var created = await SlotStream.Create(
            _file, heap, _slots[index], _offsets[index], _datasetAccess
        ).ConfigureAwait(false);

        _slotStreamCache[index] = created;

        return created;
    }

    public async ValueTask ReadDataset(Memory<byte> buffer)
    {
        var offset = 0;
        var remaining = buffer.Length;

        while (remaining > 0)
        {
            if (_slotStream is null || _loadSlot)
            {
                var index = Enumerable.Range(0, _offsets.Length).Last(i => _offsets[i] <= _position);
                _slotStream = await GetOrCreateSlotStream(index).ConfigureAwait(false);
                _slotStream.Seek(_position - _offsets[index], SeekOrigin.Begin);
                _loadSlot = false;
            }

            var streamRemaining = _slotStream.Length - _slotStream.Position;

            if (streamRemaining <= 0)
                throw new Exception("The current stream has already been consumed.");

            var length = (int)Math.Min(remaining, streamRemaining);

            await _slotStream.ReadDataset(buffer.Slice(offset, length)).ConfigureAwait(false);
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
            foreach (var stream in _slotStreamCache)
            {
                stream?.Dispose();
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
