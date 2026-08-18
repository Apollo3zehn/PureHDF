namespace PureHDF.VFD;

/// <summary>
/// A single sliding window of recently fetched file bytes, used to coalesce the reader's many small
/// structural reads into few underlying fetches.
/// </summary>
/// <remarks>
/// This is COALESCING, not caching, and the distinction is the whole design. The library already
/// caches - object headers, local heaps, b-tree and fractal heap headers and their nodes, chunk
/// indexes and global heap collections are all retained in decoded form, keyed by file address (see
/// NativeCache and BoundedAddressCache). What those caches cannot help with is the FIRST read of a
/// structure, because the reader decodes field by field: a byte here, two bytes there, and one
/// interface call per field.
/// <para>
/// MEASURED, on one dense attribute lookup with every structure cache already warm: 484 reads
/// moving 1,828 bytes - 3.8 bytes per read. 303 of those reads are a single byte, and 272 of them
/// are byte-at-a-time scans of null-terminated strings (27 of them: the attribute name plus the
/// member names of its compound datatype). Those 484 reads form just TWO forward-contiguous runs,
/// of 486 and 1,342 bytes, so a window of a few KiB collapses the whole lookup into two fetches.
/// </para>
/// <para>
/// Why this belongs in the driver rather than in the caller's stream. It is tempting to leave it to
/// an <see cref="IDatasetStream" /> implementation - <c>AmazonS3Stream</c> does exactly that with
/// 1 MiB slots - but that only helps a source that IS a stream. H5FileHandleDriver reads through
/// <c>RandomAccess.Read</c>, which is positional and therefore BYPASSES the FileStream buffer
/// entirely, so on a local file each of those reads is its own pread. (Derived, not traced: no
/// syscall counter was available here, but the mapping is one read to one pread, and the read counts
/// themselves are measured - see NavigationCostTests.) There is no stream anywhere in that path to
/// fix it in. Putting the window here fixes every driver at once, and composes with a caching stream
/// rather than duplicating it: the stream still owns large-block transport caching, which is the part
/// that depends on round-trip latency and bandwidth - things this layer cannot know.
/// </para>
/// <para>
/// It needs NO synchronization, NO eviction policy and NO invalidation, which is what keeps it
/// small: a driver is owned by exactly one logical reader (see H5DriverBase), a window holds one
/// range rather than a collection, and an HDF5 file open for reading does not change underneath it.
/// Contrast the two stream-side caches, which had to get all three right - and did not.
/// </para>
/// <para>
/// Only STRUCTURAL reads come through here. Bulk dataset payload is read straight into the caller's
/// buffer: it is large, read once, and would evict the structure this exists to hold.
/// </para>
/// </remarks>
internal sealed class ReadAheadWindow
{
    /// <summary>
    /// The window size, and therefore the largest read that can be served from it.
    /// </summary>
    /// <remarks>
    /// One page, and large enough for the structures that are actually walked field by field: the
    /// two contiguous runs measured above are 486 and 1,342 bytes, a b-tree v2 leaf for a rank-2
    /// chunk index measures ~3.1 KiB, and object header chunks and fractal heap direct blocks are
    /// of that order too.
    /// <para>
    /// Deliberately not larger. The window is refilled on every miss, so its size is also the read
    /// amplification of a scattered access pattern - a lookup that touches one field in each of two
    /// distant structures fetches two full windows. At 4 KiB that is a fair trade against a syscall
    /// or a round trip; at 64 KiB it would not be. A source that genuinely wants large blocks has
    /// somewhere better to put them: an IDatasetStream implementation, which knows its own latency
    /// and bandwidth, whereas this layer knows neither.
    /// </para>
    /// </remarks>
    public const int DefaultSize = 4096;

    private readonly int _size;

    // Allocated on the first refill, not in the constructor: a driver that only ever reads bulk
    // payload never touches this, and the read path allocates a driver per read operation.
    private byte[]? _buffer;

    // The absolute file offset of _buffer[0]. Negative means "holds nothing".
    private long _start = -1;

    // Valid bytes in _buffer. May be less than _size near the end of the file.
    private int _length;

    public ReadAheadWindow(int size = DefaultSize)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "The window size must be positive.");

        _size = size;
    }

    /// <summary>
    /// Copies <paramref name="destination" />.Length bytes from the window into
    /// <paramref name="destination" />, if the window holds all of them.
    /// </summary>
    /// <remarks>
    /// A read BEHIND the current position hits just as well as one ahead of it, because the window
    /// is keyed by absolute offset and not by a cursor. That is load-bearing rather than incidental:
    /// <c>ReadUtils.ReadNullTerminatedString</c> overshoots the terminator and then seeks back over
    /// the padding, and several decoders seek backwards over a field they had to look ahead at.
    /// </remarks>
    public bool TryServe(long position, Span<byte> destination)
    {
        if (_buffer is null || _start < 0)
            return false;

        var offset = position - _start;

        if (offset < 0 || offset + destination.Length > _length)
            return false;

        _buffer.AsSpan((int)offset, destination.Length).CopyTo(destination);

        return true;
    }

    /// <summary>
    /// Returns how many bytes to fetch in order to serve a <paramref name="count" />-byte read at
    /// <paramref name="position" />, or 0 when the caller should read straight into its own buffer
    /// and leave the window alone.
    /// </summary>
    /// <remarks>
    /// Two cases bypass. A read at least as large as the window cannot be served from it and would
    /// only displace what it holds - the attribute payload read at the end of every
    /// <c>AttributeMessage.Decode</c> is the common instance. And a read that already reaches the
    /// end of the file has nothing to read ahead OF, so buffering it would just add a copy; folding
    /// that case in here also means a refill can never be asked for bytes past the end, which the
    /// exact-read contract of <see cref="IDatasetStream.ReadMetadataAsync" /> would reject.
    /// </remarks>
    public int GetRefillLength(int count, long position, long sourceLength)
    {
        if (count >= _size)
            return 0;

        var available = sourceLength - position;

        if (available <= count)
            return 0;

        return (int)Math.Min(_size, available);
    }

    /// <summary>
    /// Returns the buffer to fetch <paramref name="length" /> bytes into, and marks the window empty
    /// until <see cref="CompleteRefill" /> accepts them.
    /// </summary>
    /// <remarks>
    /// Empty in between, deliberately: the fetch may be awaited, and it may throw. Either way the
    /// window must not appear to hold bytes it does not have, and a failed refill leaves it holding
    /// nothing rather than something stale.
    /// </remarks>
    public Memory<byte> BeginRefill(int length)
    {
        _buffer ??= new byte[_size];

        Invalidate();

        return new Memory<byte>(_buffer, 0, length);
    }

    /// <summary>
    /// Publishes the bytes written by the fetch that <see cref="BeginRefill" /> started.
    /// </summary>
    public void CompleteRefill(long start, int length)
    {
        _start = start;
        _length = length;
    }

    /// <summary>
    /// Drops whatever the window holds.
    /// </summary>
    public void Invalidate()
    {
        _start = -1;
        _length = 0;
    }
}
