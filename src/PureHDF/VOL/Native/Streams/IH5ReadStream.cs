namespace PureHDF;

internal interface IH5ReadStream : IDisposable
{
    long Position { get; }

    // Seek stays synchronous on purpose: it is pure arithmetic for an in-memory or
    // range-backed source and never needs IO.
    void Seek(long offset, SeekOrigin origin);

    // Memory<byte> rather than Span<byte>: a Span cannot cross an await boundary.
    ValueTask ReadDatasetAsync(Memory<byte> buffer);

    /// <summary>
    ///     Reads into <paramref name="buffer" /> without suspending, or returns <c>false</c> if this
    ///     source cannot serve the read synchronously (in which case the caller must go through
    ///     <see cref="ReadDatasetAsync" />).
    /// </summary>
    /// <remarks>
    ///     Answers: <em>can I avoid <see cref="CastMemoryManager{T, U}" />?</em> - a Span-vs-Memory
    ///     question about allocation. <see cref="ReadDatasetAsync" /> takes <c>Memory&lt;byte&gt;</c>
    ///     because a <c>Span</c> cannot cross an <c>await</c> - but reinterpreting a
    ///     <c>Memory&lt;T&gt;</c> as <c>Memory&lt;byte&gt;</c> requires a heap-allocated
    ///     <see cref="CastMemoryManager{T, U}" /> per call, whereas <c>MemoryMarshal.AsBytes</c> over
    ///     a <c>Span</c> is free. Most sources on the read path are already fully in memory (a decoded
    ///     chunk, an attribute's bytes, a memory-mapped view) and never had any reason to suspend, so
    ///     they take the Span overload and allocate nothing.
    ///     <para>
    ///         The default is <c>false</c>: a source is assumed remote until it says otherwise, so a
    ///         genuinely asynchronous stream can never be accidentally blocked on.
    ///     </para>
    /// </remarks>
    bool TryReadDatasetSync(Span<byte> buffer) => false;

    /// <summary>
    ///     <c>true</c> when this stream is backed by an in-memory buffer, so every read is a plain
    ///     memory copy with no IO and no per-call dispatch cost.
    /// </summary>
    /// <remarks>
    ///     Answers: <em>would batching be redundant?</em> - a dispatch-cost question (N reads vs 1).
    ///     Decoders that loop over <see cref="ReadDatasetAsync" /> per element use this to decide
    ///     whether to pre-read the whole batch into a pooled buffer and decode from an in-memory
    ///     <see cref="SystemMemoryStream" /> wrapper (collapsing N small driver reads into one). For
    ///     a source that is already in memory that bulk read would just duplicate the copy, so the
    ///     per-element decode runs against the original stream instead.
    ///     <para>
    ///         The default is <c>false</c>: assume a live backing store (a file/stream/mmap driver,
    ///         an external file) until the stream opts in. A memory-mapped driver is not
    ///         <em>buffered</em> here - its data is reached through the driver read API, not exposed
    ///         as a contiguous <c>Memory&lt;byte&gt;</c>, so batching its dispatch still helps.
    ///     </para>
    /// </remarks>
    bool IsBuffered => false;
}
