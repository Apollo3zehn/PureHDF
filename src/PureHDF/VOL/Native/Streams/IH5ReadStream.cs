namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void ReadDataset(Memory<byte> buffer);
}