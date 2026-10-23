using System.Buffers;

namespace PureHDF;

/// <summary>
/// Writes a dataset's payload to the file at a fixed base address, gathering the small writes an encode
/// makes into larger ones.
/// </summary>
/// <remarks>
/// An encoder writes element by element - two writes of 4 and 12 bytes for each variable-length value.
/// Passed straight through, every one of them is a call on the destination stream, which only a buffered
/// file stream absorbs. Gathered here, a run of consecutive writes reaches the stream as one.
/// <para>
/// The stream keeps its own position and seeks the driver to it when it writes out, so whatever else
/// moves the driver in the meantime - an object an encoded reference points at - cannot misplace it.
/// Nothing is held across encodes: <see cref="Flush" /> writes everything out and gives the buffer back,
/// so a stream kept by a dataset waiting for more data costs nothing between writes.
/// </para>
/// </remarks>
internal sealed class PayloadWriteStream : IH5WriteStream
{
    private const int BufferSize = 64 * 1024;

    private readonly H5DriverBase _driver;
    private readonly long _baseAddress;

    private byte[]? _buffer;
    private int _buffered;
    private long _bufferStart;
    private long _position;

    /// <param name="driver">The driver to write to, positioned at the payload's first byte.</param>
    public PayloadWriteStream(H5DriverBase driver)
    {
        _driver = driver;
        _baseAddress = driver.Position;
    }

    public long Position => _position;

    public void Seek(long offset, SeekOrigin origin)
    {
        var position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => throw new Exception($"Seek origin '{origin}' is not supported.")
        };

        // A seek that does not move continues the run being gathered.
        if (position != _position)
        {
            Flush();
            _position = position;
        }
    }

    public void WriteDataset(Span<byte> buffer)
    {
        if (buffer.Length >= BufferSize)
        {
            Flush();
            WriteOut(_position, buffer);
            _position += buffer.Length;

            return;
        }

        if (_buffered + buffer.Length > BufferSize)
            Flush();

        if (_buffered == 0)
        {
            _buffer ??= ArrayPool<byte>.Shared.Rent(BufferSize);
            _bufferStart = _position;
        }

        buffer.CopyTo(_buffer.AsSpan(_buffered));
        _buffered += buffer.Length;
        _position += buffer.Length;
    }

    public void Flush()
    {
        if (_buffer is null)
            return;

        if (_buffered > 0)
            WriteOut(_bufferStart, _buffer.AsSpan(0, _buffered));

        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = null;
        _buffered = 0;
    }

    public void Dispose()
    {
        Flush();
    }

    private void WriteOut(long position, Span<byte> data)
    {
        _driver.Seek(_baseAddress + position, SeekOrigin.Begin);
        _driver.WriteDataset(data);
    }
}
