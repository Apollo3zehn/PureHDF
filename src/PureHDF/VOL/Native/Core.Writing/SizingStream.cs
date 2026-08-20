namespace PureHDF.VOL.Native;

/// <summary>
/// A seekable stream that discards everything written to it and reads back zeroes, used by the sizing
/// pass that decides how much space to reserve for file structure.
/// </summary>
/// <remarks>
/// It tracks position and length so that the writer behaves exactly as it would against a real stream -
/// the writer allocates every address up front and then seeks to it, so nothing it does depends on the
/// bytes already there. Reads exist only because the writer verifies checksums by reading back what it
/// wrote; a checksum computed over zeroes is wrong, but a checksum's SIZE is fixed, and size is all this
/// pass is measuring.
/// <para>
/// Not a MemoryStream, deliberately: the point is to measure a file without materialising it, and a
/// 620 MB file would otherwise be buffered in memory to learn how big its structure is.
/// </para>
/// </remarks>
internal sealed class SizingStream : Stream
{
    private long _position;
    private long _length;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Advance(count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Advance(buffer.Length);
    }

    public override void WriteByte(byte value)
    {
        Advance(1);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        // Zeroes rather than a short read: the writer expects to get back as much as it asks for, and a
        // short read would send it down an error path that has nothing to do with what is being measured.
        buffer.Clear();
        _position += buffer.Length;

        return buffer.Length;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        return _position;
    }

    public override void SetLength(long value)
    {
        _length = value;
    }

    public override void Flush()
    {
        //
    }

    private void Advance(int count)
    {
        _position += count;

        if (_position > _length)
            _length = _position;
    }
}
