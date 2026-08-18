namespace PureHDF;

internal class UnsafeFillValueStream : IH5ReadStream
{
    private readonly byte[]? _fillValue;
    private readonly int _length;
    private long _position;

    public UnsafeFillValueStream(byte[]? fillValue)
    {
        _fillValue = fillValue;

        _length = _fillValue is null
            ? 1
            : _fillValue.Length;
    }

    public long Position { get => _position; }

    // No IO at all: stays sync-completing. Not async, so `fixed` over the Span is still legal.
    public ValueTask ReadDatasetAsync(Memory<byte> memory)
    {
        ReadCore(memory.Span);

        return default;
    }

    // No IO at all, so always synchronous.
    public bool TryReadDatasetSync(Span<byte> buffer)
    {
        ReadCore(buffer);

        return true;
    }

    private unsafe void ReadCore(Span<byte> buffer)
    {
        if (_fillValue is null)
        {
            buffer.Clear();
        }

        else
        {
            unsafe
            {
                fixed (byte* ptrSrc = _fillValue, ptrDst = buffer)
                {
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        ptrDst[i] = ptrSrc[(_position + i) % _length];
                    }
                }
            }

            _position += buffer.Length;
        }
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        _position += origin switch
        {
            SeekOrigin.Begin => offset,
            _ => throw new NotImplementedException(),
        };
    }

    public void Dispose()
    {
        // do nothing
    }
}
