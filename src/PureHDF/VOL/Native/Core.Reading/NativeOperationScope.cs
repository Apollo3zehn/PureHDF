namespace PureHDF.VOL.Native;

/// <summary>
/// Scopes a single read operation (one <c>NativeDataset.Read</c> / <c>NativeAttribute.Read</c>
/// call) to its own driver, so that a dataset or attribute resolved once can be read concurrently
/// from many threads through one shared <see cref="NativeFile"/>.
/// </summary>
/// <remarks>
/// A driver cursor is a plain field, so a shared driver races on its position - and it really is
/// moved during a read, not only during navigation: <c>NativeCache.GetGlobalHeapObject</c> seeks
/// the driver as a side effect while decoding variable-length data. Isolating the driver per
/// operation is what closes that.
/// <para>
/// The copied context keeps the file-level <c>CacheToken</c>, <c>ReadOptions</c>, <c>Superblock</c>
/// and <c>File</c> (a record's synthesized copy constructor copies fields rather than re-running
/// initializers), so the global-heap cache stays shared per file instead of degenerating into one
/// cache per read.
/// </para>
/// <para>
/// A struct, and <c>Context</c> falls back to the file-level context when the source cannot be
/// read concurrently, so a <c>Stream</c>-backed file pays nothing at all. Otherwise the driver and
/// context pair is taken from - and handed back to - <see cref="NativeOperationSlot" />, so a reader
/// whose reads never overlap allocates them once per file rather than once per <c>Read</c>.
/// </para>
/// </remarks>
internal readonly struct NativeOperationScope : IDisposable
{
    private readonly H5DriverBase? _operationDriver;
    private readonly NativeOperationSlot? _slot;

    public NativeOperationScope(NativeReadContext fileContext)
    {
        var slot = fileContext.OperationSlot;
        var idle = slot.TryTake();

        // Reuse. The cursor is wherever the previous operation left it, which does not matter: every
        // read path seeks before its first read. BaseAddress was copied when the driver was made and
        // never changes afterwards.
        if (idle is not null)
        {
            Context = idle;
            _operationDriver = idle.Driver;
            _slot = slot;

            return;
        }

        var operationDriver = fileContext.Driver.TryCreateOperationDriver();

        // The source cannot isolate a cursor (a Stream has exactly one). Reads through it are
        // documented as non-concurrent, so the file-level context is used as-is and there is nothing
        // to dispose or hand back.
        if (operationDriver is null)
        {
            Context = fileContext;
            _operationDriver = null;
            _slot = null;

            return;
        }

        _operationDriver = operationDriver;
        _slot = slot;
        Context = fileContext with { Driver = operationDriver };
    }

    /// <summary>
    /// The context every step of the operation must use. Reaching for the file-level context from
    /// inside an operation is the bug this type exists to prevent.
    /// </summary>
    public NativeReadContext Context { get; }

    public void Dispose()
    {
        if (_operationDriver is null)
            return;

        // Offer the pair to the next operation on this file rather than disposing it - see
        // NativeOperationSlot.Return for why losing this race is harmless.
        _slot?.Return(Context);
    }
}
