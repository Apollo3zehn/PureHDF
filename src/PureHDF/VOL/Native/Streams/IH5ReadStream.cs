namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    // Seek stays synchronous on purpose: it is pure arithmetic for an in-memory or
    // range-backed source and never needs IO.
    void Seek(long offset, SeekOrigin origin);

    // Memory<byte> rather than Span<byte>: a Span cannot cross an await boundary.
    ValueTask ReadDataset(Memory<byte> buffer);

    /// <summary>
    ///     Reads into <paramref name="buffer" /> without suspending, or returns <c>false</c> if this
    ///     source cannot serve the read synchronously (in which case the caller must go through
    ///     <see cref="ReadDataset" />).
    /// </summary>
    /// <remarks>
    ///     This exists to keep the async conversion free on the hot decode path, not as a
    ///     convenience. <see cref="ReadDataset" /> takes <c>Memory&lt;byte&gt;</c> because a
    ///     <c>Span</c> cannot cross an <c>await</c> - but reinterpreting a <c>Memory&lt;T&gt;</c> as
    ///     <c>Memory&lt;byte&gt;</c> requires a heap-allocated <see cref="CastMemoryManager{T, U}" />
    ///     per call, whereas <c>MemoryMarshal.AsBytes</c> over a <c>Span</c> is free. Most sources on
    ///     the read path are already fully in memory (a decoded chunk, an attribute's bytes, a
    ///     memory-mapped view) and never had any reason to suspend, so they take the Span overload
    ///     and allocate nothing.
    ///     <para>
    ///         The default is <c>false</c>: a source is assumed remote until it says otherwise, so a
    ///         genuinely asynchronous stream can never be accidentally blocked on.
    ///     </para>
    /// </remarks>
    bool TryReadDatasetSync(Span<byte> buffer) => false;
}
