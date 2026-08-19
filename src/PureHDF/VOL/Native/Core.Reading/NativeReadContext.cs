namespace PureHDF.VOL.Native;

internal record class NativeReadContext(
    H5DriverBase Driver,
    Superblock Superblock
)
{
    public required H5ReadOptions ReadOptions { get; init; }

    public NativeFile File { get; set; } = default!;

    // Stable per-file identity used to key the process-wide caches in NativeCache, rather than
    // the H5DriverBase instance. A driver is allocated per read operation, so keying on it would
    // give every read its own (never-cleared) cache. This token is created here,
    // once, by the primary constructor at the single place a "real" file context
    // is built (NativeFile.InternalOpenAsync). A record's "with" expression uses
    // the synthesized copy constructor, which copies the current field value
    // rather than re-running this initializer, so the token's identity survives
    // any `context with { ... }` copy.
    public NativeCacheToken CacheToken { get; init; } = new();

    // One idle per-operation driver/context pair per file, so that a reader
    // which never overlaps its reads allocates them once for the file instead of once per Read.
    // Created here for the same reason as CacheToken above, and shared by `with` copies for the same
    // reason - which is also what makes nesting safe: an outer operation has taken the pair, so an
    // inner one finds the slot empty and allocates its own.
    public NativeOperationSlot OperationSlot { get; init; } = new();
};

/// <summary>
/// Holds at most one idle read-operation context per file, for reuse by the next read.
/// </summary>
/// <remarks>
/// Isolating a driver per read operation is what makes concurrent reads correct, but allocating one
/// per <c>Read</c> costs about 110 bytes a call - which showed up as +26% on attribute scalar reads,
/// the case that allocates least per read and so feels a fixed addition most. Handing the pair back
/// after each operation makes the uncontended case free again, while overlapping reads simply miss
/// the slot and allocate, so correctness never depends on the reuse succeeding.
/// </remarks>
internal sealed class NativeOperationSlot
{
    private NativeReadContext? _idle;

    /// <summary>
    /// Takes the idle context, or returns <see langword="null" /> if another operation holds it.
    /// </summary>
    public NativeReadContext? TryTake()
    {
        return Interlocked.Exchange(ref _idle, null);
    }

    /// <summary>
    /// Offers a context for the next operation.
    /// </summary>
    /// <remarks>
    /// A plain reference store, not a compare-exchange: reference assignment is already atomic, so a
    /// taker can never observe a torn value, and the only thing a lost race costs is that the
    /// overwritten pair is never handed out again. Its driver is then collected without being
    /// disposed, which is harmless - an operation driver is constructed with
    /// <c>leaveOpen: true</c> precisely because it does not own the file handle or the accessor, so
    /// its <c>Dispose</c> releases nothing. Halving the atomics matters because this sits on the
    /// per-<c>Read</c> path, where the pair of interlocked operations was itself measurable.
    /// </remarks>
    public void Return(NativeReadContext context)
    {
        Volatile.Write(ref _idle, context);
    }
}

/// <summary>
/// Opaque per-file cache key. Only instance identity matters — two tokens are
/// never equal unless they are the same reference.
/// </summary>
internal sealed class NativeCacheToken
{
}