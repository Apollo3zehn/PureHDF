namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void Read(Memory<byte> buffer);

#if NET6_0_OR_GREATER
    ValueTask ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // test
        throw new NotImplementedException();
        Read(buffer);
    }
#endif
}