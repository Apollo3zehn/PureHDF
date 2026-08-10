namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    // Seek stays synchronous on purpose: it is pure arithmetic for an in-memory or
    // range-backed source and never needs IO.
    void Seek(long offset, SeekOrigin origin);

    // Memory<byte> rather than Span<byte>: a Span cannot cross an await boundary.
    ValueTask ReadDataset(Memory<byte> buffer);
}
