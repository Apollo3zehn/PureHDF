namespace PureHDF;

internal partial class SystemMemoryStream : IH5ReadStream
{
    private long _position;

    public SystemMemoryStream(Memory<byte> memory)
    {
        Reset(memory);
    }

    public long Position { get => _position; }

    public Memory<byte> OriginalMemory { get; private set; }

    public Memory<byte> SlicedMemory { get; private set; }

    public void ReadDataset(Memory<byte> buffer)
    {
        var length = Math.Min(SlicedMemory.Length, buffer.Length);

        SlicedMemory[..length]
            .CopyTo(buffer);

        Seek(length, SeekOrigin.Current);
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:

                if (offset > OriginalMemory.Length)
                    throw new NotSupportedException("Cannot seek behind the end of the array.");

                SlicedMemory = OriginalMemory[(int)offset..];
                _position = offset;

                break;

            case SeekOrigin.Current:

                if (offset > SlicedMemory.Length)
                    throw new NotSupportedException("Cannot seek behind the end of the array.");

                SlicedMemory = SlicedMemory[(int)offset..];
                _position += offset;

                break;

            case SeekOrigin.End:

                if (offset > SlicedMemory.Length)
                    throw new NotSupportedException("Cannot seek before the start of the array.");

                SlicedMemory = OriginalMemory[^((int)offset)..];
                _position = OriginalMemory.Length - (int)offset;

                break;

            default:
                throw new NotSupportedException("Unknown seek origin.");
        }
    }

    public void Reset(Memory<byte> memory)
    {
        OriginalMemory = memory;
        SlicedMemory = memory;
        _position = 0;
    }

    public void Dispose()
    {
        // do nothing
    }
}