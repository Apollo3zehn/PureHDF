namespace PureHDF.Tests.Reading;

/// <summary>
/// An <see cref="IDatasetStream" /> over an in-memory buffer.
/// </summary>
/// <remarks>
/// A positionless read out of a <c>byte[]</c> is inherently safe to perform from many threads at
/// once, so this contains no synchronization at all beyond the two call counters - which means
/// anything that goes wrong in <c>ConcurrencyTests</c> is the driver sharing a cursor, not the
/// stream.
/// <para>
/// The cursor-based <see cref="Stream" /> members throw. PureHDF must not reach for them once
/// <see cref="IDatasetStream" /> is implemented - that is the whole point - so any read or seek
/// still going through the stream cursor fails the test loudly instead of quietly working on this
/// one thread and racing on any other.
/// </para>
/// <para>
/// The counters make this double as the measuring instrument for <c>NavigationCostTests</c>: a read
/// count is a deterministic, machine-independent observation, unlike a timing, and structural reads
/// are separated from bulk payload reads because only the former are affected by navigation.
/// </para>
/// </remarks>
internal sealed class PositionlessDatasetStream : Stream, IDatasetStream
{
    private readonly byte[] _data;
    private readonly bool _suspend;
    private int _datasetReadCount;
    private int _metadataReadCount;
    private long _metadataBytesRead;

    public PositionlessDatasetStream(byte[] data, bool suspend)
    {
        _data = data;
        _suspend = suspend;
    }

    public int DatasetReadCount => Volatile.Read(ref _datasetReadCount);

    public int MetadataReadCount => Volatile.Read(ref _metadataReadCount);

    /// <summary>
    /// Total bytes served through <see cref="ReadMetadataAsync" />, so that a caller can tell a read count
    /// that is high because a lot of structure was read from one that is high because the same
    /// structure was read a few bytes at a time.
    /// </summary>
    public long MetadataBytesRead => Volatile.Read(ref _metadataBytesRead);

    /// <summary>
    /// Zeroes both counters, so that a caller can measure one isolated operation after warming up
    /// whatever it does not want to measure.
    /// </summary>
    public void ResetCounts()
    {
        Volatile.Write(ref _datasetReadCount, 0);
        Volatile.Write(ref _metadataReadCount, 0);
        Volatile.Write(ref _metadataBytesRead, 0);
    }

    public ValueTask ReadDatasetAsync(long offset, Memory<byte> buffer)
    {
        Interlocked.Increment(ref _datasetReadCount);

        return ReadCore(offset, buffer);
    }

    public ValueTask ReadMetadataAsync(long offset, Memory<byte> buffer)
    {
        Interlocked.Increment(ref _metadataReadCount);
        Interlocked.Add(ref _metadataBytesRead, buffer.Length);

        return ReadCore(offset, buffer);
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _data.Length;

    public override long Position
    {
        get => throw CursorUsed();
        set => throw CursorUsed();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw CursorUsed();

    public override long Seek(long offset, SeekOrigin origin) => throw CursorUsed();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Flush() => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private ValueTask ReadCore(long offset, Memory<byte> buffer)
    {
        if (_suspend)
            return SuspendThenCopy(offset, buffer);

        Copy(offset, buffer);

        return default;
    }

    private async ValueTask SuspendThenCopy(long offset, Memory<byte> buffer)
    {
        await Task.Run(() => Copy(offset, buffer)).ConfigureAwait(false);
    }

    private void Copy(long offset, Memory<byte> buffer)
    {
        if (offset < 0 || offset + buffer.Length > _data.Length)
            throw new EndOfStreamException($"Read of {buffer.Length} bytes at offset {offset} exceeds the {_data.Length} byte buffer.");

        _data.AsSpan((int)offset, buffer.Length).CopyTo(buffer.Span);
    }

    private static InvalidOperationException CursorUsed()
    {
        return new InvalidOperationException(
            "A cursor-based Stream member was used although IDatasetStream is implemented.");
    }
}
