namespace PureHDF.Tests.Reading;

/// <summary>
/// An <see cref="IConcurrentStream" /> over an in-memory buffer.
/// </summary>
/// <remarks>
/// A positionless read out of a <c>byte[]</c> is inherently safe to perform from many threads at
/// once, so this contains no synchronization at all beyond the two call counters - which means
/// anything that goes wrong in <c>ConcurrencyTests</c> is the driver sharing a cursor, not the
/// stream.
/// <para>
/// This is a bare <see cref="IConcurrentStream" /> with no <c>Stream</c> base, so there is no cursor
/// for PureHDF to reach for in the first place: every read must go through
/// <see cref="ReadDatasetAsync" /> / <see cref="ReadMetadataAsync" />. That is the shape the
/// <c>Open(IConcurrentStream, ...)</c> overload drives, and a Stream that also implements the
/// interface takes the same positionless path after the cast in <c>H5StreamDriver</c>'s Stream
/// constructor.
/// </para>
/// <para>
/// The counters make this double as the measuring instrument for <c>NavigationCostTests</c>: a read
/// count is a deterministic, machine-independent observation, unlike a timing, and structural reads
/// are separated from bulk payload reads because only the former are affected by navigation.
/// </para>
/// </remarks>
internal sealed class ConcurrentStream : IConcurrentStream
{
    private readonly byte[] _data;
    private readonly bool _suspend;
    private int _datasetReadCount;
    private int _metadataReadCount;
    private long _metadataBytesRead;

    public ConcurrentStream(byte[] data, bool suspend)
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

    public long Length => _data.Length;

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

    // Nothing to release; the buffer is owned by the caller. Required because IConcurrentStream
    // extends IDisposable so that real implementations (e.g. AmazonS3Stream) can release caches and
    // semaphores.
    public void Dispose()
    {
    }

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
}
