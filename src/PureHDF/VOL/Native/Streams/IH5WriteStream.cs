namespace PureHDF;

internal interface IH5WriteStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void Write(Span<byte> buffer);
}