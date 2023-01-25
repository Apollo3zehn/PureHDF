namespace HDF5.NET
{
    internal class ExternalFileListStream : Stream
    {
        private long _position;
        private bool _loadSlot;
        private SlotStream? _slotStream;
        private readonly SlotStream[] _slotStreams;

        public ExternalFileListStream(
            ExternalFileListMessage externalFileList,
            H5DatasetAccess datasetAccess)
        {
            var offset = 0L;

            _slotStreams = externalFileList
                .SlotDefinitions
                .Select(slot =>
                {
                    var stream = new SlotStream(externalFileList.Heap, slot, offset, datasetAccess);
                    offset += stream.Length;
                    return stream;
                })
                .ToArray();

            if (_slotStreams.Any())
            {
                Length =
                    _slotStreams.Last().Offset +
                    _slotStreams.Last().Length;
            }
            else
            {
                throw new Exception("There must at least a single file be defined in the external file list.");
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // WARNING: Original "buffer" is a Memory<byte> and to be compatible with stream,
            // the method Stream.Read uses an internal ArrayPool array which is not cleared
            // after return. This means that the buffer could contain data from previous tests.
            // Therefore, always read the returned number of bytes and do NOT SKIP any data, e.g.
            // when a file is smaller than the corresponding slot!

            var remaining = count;

            while (remaining > 0)
            {
                if (_slotStream is null || _loadSlot)
                {
                    _slotStream = _slotStreams.Last(stream => stream.Offset <= _position);
                    _slotStream.Seek(_position - _slotStream.Offset, SeekOrigin.Begin);
                    _loadSlot = false;
                }

                var streamRemaining = _slotStream.Length - _slotStream.Position;
                var length = (int)Math.Min(remaining, streamRemaining);

                _slotStream.Read(buffer, offset, length);
                _position += length;
                offset += length;
                remaining -= length;

                if (length == streamRemaining)
                    _loadSlot = true;
            }

            return count;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // WARNING: Original "buffer" is a Memory<byte> and to be compatible with stream,
            // the method Stream.Read uses an internal ArrayPool array which is not cleared
            // after return. This means that the buffer could contain data from previous tests.
            // Therefore, always read the returned number of bytes and do NOT SKIP any data, e.g.
            // when a file is smaller than the corresponding slot!

            var remaining = count;

            while (remaining > 0)
            {
                if (_slotStream is null || _loadSlot)
                {
                    _slotStream = _slotStreams.Last(stream => stream.Offset <= _position);
                    _slotStream.Seek(_position - _slotStream.Offset, SeekOrigin.Begin);
                    _loadSlot = false;
                }

                var streamRemaining = _slotStream.Length - _slotStream.Position;
                var length = (int)Math.Min(remaining, streamRemaining);

                await _slotStream.ReadAsync(buffer, offset, length).ConfigureAwait(false);
                _position += length;
                offset += length;
                remaining -= length;

                if (length == streamRemaining)
                    _loadSlot = true;
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:

                    if (offset > Length)
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

                    return _position;
            }

            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var stream in _slotStreams)
            {
                stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
