namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void ReadDataset(Memory<byte> buffer);

#if NET6_0_OR_GREATER
    ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
#endif
}