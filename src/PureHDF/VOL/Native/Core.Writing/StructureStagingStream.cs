namespace PureHDF.VOL.Native;

/// <summary>
/// Holds what is written to it in memory until <see cref="Commit" />, and passes everything through to
/// the stream it wraps from then on.
/// </summary>
/// <remarks>
/// For a front-loaded write that encodes its structure once: payload is placed after the structure, so
/// payload addresses are only final once the structure's size is known, and the structure that names
/// them has to be corrected before it reaches the file. Holding it here keeps it out of the file until
/// then, which needs no read-back, and lets it go out front to back in one run once it is correct.
/// <para>
/// Kept in segments rather than one growing array: the structure's size is not known in advance, and a
/// doubling array would hold up to twice the structure, plus a copy of it at every step, on the large
/// object heap. Segments are below the large object threshold, so they are reclaimed with the rest of
/// the young garbage once committed.
/// </para>
/// </remarks>
internal sealed class StructureStagingStream(Stream inner, bool leaveOpen) : Stream
{
    private const int SEGMENT_SIZE = 64 * 1024;

    private readonly List<byte[]?> _segments = [];

    private bool _committed;
    private long _position;
    private long _stagedStart = long.MaxValue;
    private long _stagedEnd;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _committed
        ? inner.Length
        : Math.Max(inner.Length, _stagedEnd);

    public override long Position
    {
        get => _committed ? inner.Position : _position;
        set
        {
            if (_committed)
                inner.Position = value;

            else
                _position = value;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_committed)
            return inner.Seek(offset, origin);

        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        return _position;
    }

    public override void Write(byte[] buffer, int offset, int count)
        => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_committed)
        {
            inner.Write(buffer);
            return;
        }

        if (_position + buffer.Length > FreeSpaceManager.PROVISIONAL_PAYLOAD_BASE)
            throw new InvalidOperationException("Payload was written before the structure naming it was placed.");

        WriteStaged(_position, buffer);

        _stagedStart = Math.Min(_stagedStart, _position);
        _position += buffer.Length;
        _stagedEnd = Math.Max(_stagedEnd, _position);
    }

    public override void WriteByte(byte value)
        => Write([value]);

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (!_committed)
            throw new NotSupportedException("Staged structure is not read back.");

        return inner.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_committed)
            throw new NotSupportedException("Staged structure is not read back.");

        return inner.ReadAsync(buffer, cancellationToken);
    }

    public override void SetLength(long value)
    {
        if (_committed)
        {
            inner.SetLength(value);
            return;
        }

        // Extending covers a structure's unused tail - a global heap collection's free space - which is
        // part of the region and written out as zeroes. Nothing staged is ever cut short.
        if (value < _stagedEnd || value > FreeSpaceManager.PROVISIONAL_PAYLOAD_BASE)
            throw new NotSupportedException("Staged structure can only be extended within its region.");

        _stagedEnd = value;
    }

    public override void Flush()
    {
        if (_committed)
            inner.Flush();
    }

    /// <summary>
    /// Copies staged bytes out, so they can be corrected and put back with <see cref="Patch" />.
    /// </summary>
    public void ReadStaged(long position, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            var (segment, offset) = Locate(position);
            var count = Math.Min(destination.Length, SEGMENT_SIZE - offset);
            var source = _segments[segment];

            if (source is null)
                destination[..count].Clear();

            else
                source.AsSpan(offset, count).CopyTo(destination);

            destination = destination[count..];
            position += count;
        }
    }

    /// <summary>
    /// Overwrites staged bytes in place.
    /// </summary>
    public void Patch(long position, ReadOnlySpan<byte> source)
    {
        if (_committed || position < _stagedStart || position + source.Length > _stagedEnd)
            throw new InvalidOperationException("Only staged bytes can be patched.");

        WriteStaged(position, source);
    }

    /// <summary>
    /// Writes the staged bytes to the wrapped stream, front to back, and passes everything through from
    /// then on.
    /// </summary>
    public void Commit()
    {
        if (_committed)
            return;

        if (_stagedStart < _stagedEnd)
        {
            inner.Seek(_stagedStart, SeekOrigin.Begin);

            var position = _stagedStart;
            var zeroes = default(byte[]);

            while (position < _stagedEnd)
            {
                var (segment, offset) = Locate(position);
                var count = (int)Math.Min(_stagedEnd - position, SEGMENT_SIZE - offset);
                var source = _segments[segment];

                if (source is null)
                {
                    zeroes ??= new byte[SEGMENT_SIZE];
                    inner.Write(zeroes, 0, count);
                }

                else
                {
                    inner.Write(source, offset, count);
                }

                position += count;
            }
        }

        _segments.Clear();
        _segments.TrimExcess();
        _committed = true;

        inner.Seek(_position, SeekOrigin.Begin);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            inner.Dispose();

        base.Dispose(disposing);
    }

    private void WriteStaged(long position, ReadOnlySpan<byte> source)
    {
        while (!source.IsEmpty)
        {
            var (segment, offset) = Locate(position);

            while (_segments.Count <= segment)
                _segments.Add(null);

            var target = _segments[segment] ??= new byte[SEGMENT_SIZE];
            var count = Math.Min(source.Length, SEGMENT_SIZE - offset);

            source[..count].CopyTo(target.AsSpan(offset));

            source = source[count..];
            position += count;
        }
    }

    private static (int Segment, int Offset) Locate(long position)
        => ((int)(position / SEGMENT_SIZE), (int)(position % SEGMENT_SIZE));
}
