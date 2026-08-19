using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.VFD;

// CONCURRENCY: this driver has two modes, chosen by which constructor is called - there is no
// runtime switch.
//
// POSITIONLESS MODE - the IConcurrentStream constructor. Every read on that interface carries its
// own absolute offset, so the source has no cursor for two readers to fight over. The cursor
// therefore lives in THIS driver (_position), the source is never seeked, and
// CreateOperationDriverCore hands out a second driver over the same source with its own cursor - so
// a dataset resolved once can be read concurrently through one shared H5File, exactly as for a file
// handle or a memory-mapped accessor.
//
// CURSOR MODE - the Stream constructor. Reads go through Stream.Seek plus Stream.ReadAsync, which is
// the only thing a plain Stream offers. A second driver would share that one cursor rather than
// isolate from it, so CreateOperationDriverCore returns null and reads stay non-concurrent. This is
// the mode every in-tree use takes (the local MemoryStream drivers for heap IDs and virtual-dataset
// payload, and the writer), and it is unchanged.
//
// A Stream that also implements IConcurrentStream is NOT automatically promoted to positionless mode
// when passed to the Stream constructor: the caller picks the mode by picking the constructor.
// (Positionless mode decouples the driver cursor _position from Stream.Position, which would
// silently break the writer, whose Seek must move the same cursor its Write lands at.) A caller
// who wants concurrency for such a Stream passes it to H5File.Open as an IConcurrentStream.
//
// Note that positionless mode does NOT make reads synchronous: TryReadDatasetSync stays false in both
// modes (inherited from H5DriverBase), because a source may genuinely suspend and blocking on it is
// what the async read path exists to avoid.
internal partial class H5StreamDriver : H5DriverBase
{
    private readonly bool _leaveOpen;

    // Non-null in cursor mode (Stream constructor); null in positionless mode. In cursor mode this
    // is the read source and carries Length/Position/Dispose.
    private readonly Stream? _stream;

    // Non-null exactly in positionless mode (IConcurrentStream constructor). Holding the interface
    // rather than re-testing the source on every read also makes the mode a single, obvious branch
    // at each call site.
    private readonly IConcurrentStream? _concurrentStream;

    // Positionless mode only. In cursor mode the wrapped stream's own cursor is the position and this
    // field is unused.
    private long _position;

    // COALESCING, in positionless mode only - see ReadAheadWindow. Non-null exactly when
    // _concurrentStream is, and for the same reason the two modes exist at all: positionless mode
    // means a source that carries its own offsets, which in practice means a remote one where every
    // read is a round trip. Cursor mode is taken by the in-tree MemoryStream drivers (fractal heap
    // IDs, virtual dataset payload) and by a plain FileStream handed to H5File.Open, all of which
    // already serve a small read out of memory - a window there would add a copy and save nothing.
    //
    // It also means the window is never live while WRITING, since the writer uses the Stream
    // constructor (cursor mode). That is what excuses the absence of write invalidation here;
    // H5StreamDriver.Writing keeps the invariant explicit.
    private readonly ReadAheadWindow? _readAhead;

    /// <param name="stream">The stream to read from.</param>
    /// <param name="leaveOpen">Whether <see cref="Dispose(bool)" /> leaves the stream open.</param>
    public H5StreamDriver(Stream stream, bool leaveOpen)
    {
        if (!stream.CanRead)
            throw new Exception("The stream must be readable.");

        if (!stream.CanSeek)
            throw new Exception("The stream must be seekable.");

        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Constructs a positionless-only driver over a bare <see cref="IConcurrentStream" /> with no
    /// <see cref="Stream" /> base. Every read goes through the interface; the driver owns its own
    /// cursor and never seeks. This is the read-only counterpart of the Stream constructor: there is
    /// no cursor mode and no writing, because neither is expressible without a Stream.
    /// </summary>
    /// <param name="concurrentStream">The concurrent stream to read from.</param>
    /// <param name="leaveOpen">Whether <see cref="Dispose(bool)" /> leaves the source open.</param>
    public H5StreamDriver(IConcurrentStream concurrentStream, bool leaveOpen)
    {
        _concurrentStream = concurrentStream;
        _leaveOpen = leaveOpen;
        _readAhead = new ReadAheadWindow();
    }

    // CONCURRENCY: see the mode note above. leaveOpen: true, because the source belongs to the
    // H5File and outlives the operation. Only positionless mode can hand out an isolated driver;
    // cursor mode returns null and reads stay non-concurrent.
    protected override H5DriverBase? CreateOperationDriverCore()
    {
        return _concurrentStream is null
            ? null
            : new H5StreamDriver(_concurrentStream, leaveOpen: true);
    }

    public override long Position
    {
        get => _concurrentStream is null
            ? _stream!.Position
            : _position;
    }

    public override long Length => _stream is not null
        ? _stream.Length
        : _concurrentStream!.Length;

    public override void Seek(long offset, SeekOrigin seekOrigin)
    {
        switch (seekOrigin)
        {
            case SeekOrigin.Begin:

                // In positionless mode the source is deliberately NOT touched: its cursor is
                // shared with every other driver over it, and moving it would defeat the isolation.
                if (_concurrentStream is null)
                    _stream!.Seek(offset, SeekOrigin.Begin);

                else
                    _position = offset;

                break;

            case SeekOrigin.Current:

                if (_concurrentStream is null)
                    _stream!.Seek(offset, SeekOrigin.Current);

                else
                    _position += offset;

                break;

            default:
                throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
        };
    }

    public override ValueTask ReadDatasetAsync(Memory<byte> buffer)
    {
        // Returning the callee's ValueTask directly, rather than `async`/`await`-ing it, avoids
        // building a state machine here just to forward a result.
        if (_concurrentStream is null)
            return _stream!.ReadExactlyAsyncCompat(buffer);

        // The cursor is advanced before the read is issued, not after it completes: the returned
        // ValueTask is handed straight to the caller, so there is no continuation here in which to do
        // it - and a caller that issues the next read after awaiting this one must still see the
        // advanced position.
        var offset = _position;
        _position += buffer.Length;

        return _concurrentStream.ReadDatasetAsync(offset, buffer);
    }

    public override ValueTask Read(Memory<byte> buffer)
    {
        return ReadMetadataCore(buffer);
    }

    public override ValueTask<byte> ReadByte()
    {
        return ReadScalar<byte>();
    }

    public override ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        var pending = ReadMetadataCore(buffer);

        if (pending.IsCompletedSuccessfully)
            return new ValueTask<byte[]>(buffer);

        return AwaitBytes(pending, buffer);

        static async ValueTask<byte[]> AwaitBytes(ValueTask pending, byte[] buffer)
        {
            await pending.ConfigureAwait(false);

            return buffer;
        }
    }

    public override ValueTask<ushort> ReadUInt16()
    {
        return ReadScalar<ushort>();
    }

    public override ValueTask<short> ReadInt16()
    {
        return ReadScalar<short>();
    }

    public override ValueTask<uint> ReadUInt32()
    {
        return ReadScalar<uint>();
    }

    public override ValueTask<ulong> ReadUInt64()
    {
        return ReadScalar<ulong>();
    }

    // Every structural read funnels through here, and NOT through ReadDataset: the distinction is
    // load-bearing. ReadDataset means "this is actual data", which is what lets an implementation
    // cache these small, endlessly repeated reads while streaming bulk payload uncached. Routing
    // metadata through ReadDataset would compile and read correct bytes, and destroy that signal.
    //
    // Being the single funnel is also what makes one read-ahead window enough to cover the whole
    // decode of an object header, a b-tree node or an attribute message.
    private ValueTask ReadMetadataCore(Memory<byte> buffer)
    {
        if (_concurrentStream is null)
            return _stream!.ReadExactlyAsyncCompat(buffer);

        // A hit completes synchronously and allocates nothing, which matters beyond saving the round
        // trip: it means ReadScalar's IsCompletedSuccessfully fast path is taken, so the hundreds of
        // per-field reads in a decode stop building state machines as well.
        if (_readAhead!.TryServe(_position, buffer.Span))
        {
            _position += buffer.Length;

            return default;
        }

        var refillLength = _readAhead.GetRefillLength(buffer.Length, _position, Length);

        if (refillLength == 0)
        {
            // Advanced before issuing the read, for the reason given in ReadDataset.
            var bypassOffset = _position;
            _position += buffer.Length;

            return _concurrentStream.ReadMetadataAsync(bypassOffset, buffer);
        }

        return RefillThenServe(buffer, refillLength);
    }

    // The cursor is advanced only once the refill has landed - unlike the paths above, which have no
    // continuation in which to do it. A caller awaiting this still observes the advanced position
    // before it can issue its next read, and a refill that throws leaves the cursor where it was.
    private async ValueTask RefillThenServe(Memory<byte> buffer, int refillLength)
    {
        var offset = _position;
        var window = _readAhead!.BeginRefill(refillLength);

        await _concurrentStream!.ReadMetadataAsync(offset, window).ConfigureAwait(false);

        // ReadMetadata fills its buffer completely or throws, so the window holds exactly
        // refillLength bytes - and GetRefillLength only returns a length that covers the caller. So
        // this cannot legitimately fail; it is checked because the failure mode of ignoring it is a
        // silently UNFILLED buffer, which the caller would decode as uninitialized memory.
        _readAhead.CompleteRefill(offset, window.Length);

        if (!_readAhead.TryServe(offset, buffer.Span))
            throw new Exception("The read-ahead window failed to serve a read it had just been filled for.");

        _position = offset + buffer.Length;
    }

    // A Span cannot cross an await, so a scalar read cannot use `stackalloc`. It does not
    // need a pooled buffer either: the concurrency model gives each driver instance a single logical
    // reader (see H5FileHandleDriver), and in positionless mode a concurrent reader gets a driver of
    // its own from CreateOperationDriverCore - so one 8-byte field serves every scalar read on this
    // driver.
    //
    // The method is not `async`. When the underlying stream satisfies the read without suspending -
    // always, for a MemoryStream - no state machine is built and nothing is allocated. Only a stream
    // that genuinely suspends (an HTTP range-request stream) pays for one, via ReadScalarSlow.
    private readonly byte[] _scalarBuffer = new byte[8];

    private ValueTask<T> ReadScalar<T>() where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var buffer = new Memory<byte>(_scalarBuffer, 0, size);
        var pending = ReadMetadataCore(buffer);

        if (pending.IsCompletedSuccessfully)
            return new ValueTask<T>(MemoryMarshal.Cast<byte, T>(buffer.Span)[0]);

        return ReadScalarSlow<T>(pending, buffer);
    }

    private static async ValueTask<T> ReadScalarSlow<T>(
        ValueTask pending,
        Memory<byte> buffer) where T : unmanaged
    {
        await pending.ConfigureAwait(false);

        return MemoryMarshal.Cast<byte, T>(buffer.Span)[0];
    }

    private bool _disposedValue;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!_disposedValue)
        {
            if (disposing)
            {
                if (!_leaveOpen)
                {
                    // When _stream is non-null it IS the source (which may also implement
                    // IConcurrentStream), so disposing it disposes both. When _stream is null the
                    // source is a bare IConcurrentStream with no Stream base, so dispose that.
                    if (_stream is not null)
                        _stream.Dispose();

                    else
                        _concurrentStream?.Dispose();
                }
            }

            _disposedValue = true;
        }
    }
}
