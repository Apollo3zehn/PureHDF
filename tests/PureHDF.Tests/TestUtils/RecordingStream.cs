namespace PureHDF.Tests;

/// <summary>
///     A write-only stream that records how it is written: how often each byte, the gaps behind the
///     furthest write, and how many write calls there are and how many of them jump.
/// </summary>
internal sealed class RecordingStream : MemoryStream
{
    private byte[] _counts = new byte[64 * 1024];
    private long _nextPosition = -1;

    /// <summary>Write calls.</summary>
    public long Writes { get; private set; }

    /// <summary>Writes that do not continue where the one before ended.</summary>
    public long Jumps { get; private set; }

    public override bool CanRead => false;

    // Stream.Write(ReadOnlySpan<byte>) on a derived MemoryStream routes here, so this is the only
    // write path apart from WriteByte.
    public override void Write(byte[] buffer, int offset, int count)
    {
        Record(Position, count);
        base.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        Record(Position, 1);
        base.WriteByte(value);
    }

    // Everything written so far, as disjoint ranges; the gaps between them are the holes.
    private readonly List<(long Start, long End)> _touched = [];

    /// <summary>The most gaps behind the furthest write at any one time.</summary>
    public int MostHoles { get; private set; }

    /// <summary>The most bytes in those gaps at any one time.</summary>
    public long MostHoleBytes { get; private set; }

    private long _touchedBytes;

    private void Record(long position, int count)
    {
        var end = position + count;

        Writes++;

        if (position != _nextPosition)
            Jumps++;

        _nextPosition = end;

        Touch(position, end);

        if (end > _counts.Length)
            Array.Resize(ref _counts, (int)Math.Max(end, _counts.Length * 2L));

        for (var i = position; i < end; i++)
        {
            if (_counts[i] < byte.MaxValue)
                _counts[i]++;
        }
    }

    private void Touch(long start, long end)
    {
        var index = _touched.FindIndex(range => range.End >= start);

        if (index < 0)
            index = _touched.Count;

        while (index < _touched.Count && _touched[index].Start <= end)
        {
            start = Math.Min(start, _touched[index].Start);
            end = Math.Max(end, _touched[index].End);
            _touchedBytes -= _touched[index].End - _touched[index].Start;
            _touched.RemoveAt(index);
        }

        _touched.Insert(index, (start, end));
        _touchedBytes += end - start;

        var holes = _touched.Count - 1 + (_touched[0].Start > 0 ? 1 : 0);
        MostHoles = Math.Max(MostHoles, holes);
        MostHoleBytes = Math.Max(MostHoleBytes, _touched[^1].End - _touchedBytes);
    }

    public List<(long Offset, long Length)> Ranges(Func<byte, bool> predicate)
    {
        var ranges = new List<(long, long)>();
        var length = Length;
        long? start = null;

        for (long i = 0; i < length; i++)
        {
            var matches = predicate(i < _counts.Length ? _counts[i] : (byte)0);

            if (matches && start is null)
                start = i;

            else if (!matches && start is not null)
            {
                ranges.Add((start.Value, i - start.Value));
                start = null;
            }
        }

        if (start is not null)
            ranges.Add((start.Value, length - start.Value));

        return ranges;
    }
}
