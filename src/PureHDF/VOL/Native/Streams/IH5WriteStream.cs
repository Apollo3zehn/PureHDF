namespace PureHDF;

internal interface IH5WriteStream : IDisposable
{
    long Position { get; }

    void Seek(long offset, SeekOrigin origin);

    void WriteDataset(Span<byte> buffer);

    /// <summary>
    /// Writes out whatever the stream holds back. Called once an encode is done with the stream.
    /// </summary>
    void Flush()
    {
        // Nothing is held back by default.
    }
}
