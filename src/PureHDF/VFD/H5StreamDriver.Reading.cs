using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.VFD;

// CONCURRENCY: this driver deliberately does NOT override CreateOperationDriverCore. A Stream has
// exactly one cursor and no positionless read API, so a second driver over the same Stream would
// share that cursor rather than isolate from it. TryCreateOperationDriver therefore keeps returning
// null here and reads through a Stream stay non-concurrent.
internal partial class H5StreamDriver : H5DriverBase
{
    private readonly bool _leaveOpen;
    private readonly Stream _stream;

    public H5StreamDriver(Stream stream, bool leaveOpen)
    {
        if (!stream.CanRead)
            throw new Exception("The stream must be readable.");

        if (!stream.CanSeek)
            throw new Exception("The stream must be seekable.");

        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    public override long Position { get => _stream.Position; }

    public override long Length => _stream.Length;

    public override void Seek(long offset, SeekOrigin seekOrigin)
    {
        switch (seekOrigin)
        {
            case SeekOrigin.Begin:
                _stream.Seek(offset, SeekOrigin.Begin);
                break;

            case SeekOrigin.Current:
                _stream.Seek(offset, SeekOrigin.Current);
                break;

            default:
                throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
        };
    }

    public override ValueTask ReadDataset(Memory<byte> buffer)
    {
        // Returning the callee's ValueTask directly, rather than `async`/`await`-ing it, avoids
        // building a state machine here just to forward a result.
        //
        // ReadExactlyAsyncCompat, never a hand-rolled loop: it advances the buffer it reads into and
        // treats a zero-byte read as end of stream. Getting either wrong is silent data corruption on
        // any stream that returns partial reads.
        return _stream is IDatasetStream datasetStream
            ? datasetStream.ReadDataset(buffer)
            : _stream.ReadExactlyAsyncCompat(buffer);
    }

    public override ValueTask Read(Memory<byte> buffer)
    {
        return _stream.ReadExactlyAsyncCompat(buffer);
    }

    public override ValueTask<byte> ReadByte()
    {
        return ReadScalar<byte>();
    }

    public override ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        var pending = _stream.ReadExactlyAsyncCompat(buffer);

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

    // A Span cannot cross an await, so a scalar read can no longer use `stackalloc`. It does not
    // need a pooled buffer either: the concurrency model gives each driver instance a single logical
    // reader (see H5FileHandleDriver), so one 8-byte field serves every scalar read on this driver.
    //
    // The method is not `async`. When the underlying stream satisfies the read without suspending -
    // always, for a MemoryStream - no state machine is built and nothing is allocated. Only a stream
    // that genuinely suspends (an HTTP range-request stream) pays for one, via ReadScalarSlow.
    private readonly byte[] _scalarBuffer = new byte[8];

    private ValueTask<T> ReadScalar<T>() where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var buffer = new Memory<byte>(_scalarBuffer, 0, size);
        var pending = _stream.ReadExactlyAsyncCompat(buffer);

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
                    _stream.Dispose();
            }

            _disposedValue = true;
        }
    }
}
