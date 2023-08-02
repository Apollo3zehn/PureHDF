namespace PureHDF;

internal interface IH5WriteStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void WriteDataset(Memory<byte> buffer);

#if NET6_0_OR_GREATER
    ValueTask WriteDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
#endif
}