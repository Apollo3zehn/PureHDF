namespace PureHDF.VOL.Native;

/// <summary>
/// Scopes a single read or navigation operation - one <c>NativeDataset.Read</c>,
/// <c>NativeAttribute.Read</c>, <c>NativeGroup.Get</c>, <c>Children</c>, <c>LinkExists</c>,
/// <c>NativeObject.Attributes</c>, ... - to its own driver, so that one shared
/// <see cref="NativeFile"/> can be both navigated and read from many threads at once.
/// </summary>
/// <remarks>
/// A driver cursor is a plain field, so a shared driver races on its position - and it really is
/// moved during a read, not only during navigation: <c>NativeCache.GetGlobalHeapObject</c> seeks
/// the driver as a side effect while decoding variable-length data. Isolating the driver per
/// operation is what closes that.
/// <para>
/// One scope per public call, never one per step: a path walk, a b-tree traversal, a recursive
/// reference search and a link enumeration all run on the single driver their entry point took. The
/// counterpart obligation is that nothing built inside an operation and RETAINED past it may hold on
/// to the scoped context. The retained object-header messages (<c>LinkInfoMessage</c>,
/// <c>AttributeInfoMessage</c>, <c>SymbolTableMessage</c>, <c>ExternalFileListMessage</c>) and
/// <c>ObjectHeaderScratchPad</c> therefore carry no context at all and take one per call instead,
/// while objects handed back to the caller (<c>NativeGroup</c> / <c>NativeDataset</c> /
/// <c>NativeAttribute</c>) keep the FILE-LEVEL context and open a fresh scope on each of their own
/// calls.
/// </para>
/// <para>
/// The copied context keeps the file-level <c>CacheToken</c>, <c>ReadOptions</c>, <c>Superblock</c>
/// and <c>File</c> (a record's synthesized copy constructor copies fields rather than re-running
/// initializers), so the global-heap cache stays shared per file instead of degenerating into one
/// cache per read.
/// </para>
/// <para>
/// A struct, and <c>Context</c> falls back to the file-level context when the source cannot be
/// read concurrently, so a file behind a plain cursor-based <c>Stream</c> pays nothing at all. (A
/// <c>Stream</c> implementing <see cref="IDatasetStream" /> reads by absolute offset and does
/// isolate, so it takes the path below like any file handle.) Otherwise the driver and
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
        // read path seeks before its first read. BaseAddress is set when the driver is created and
        // never changes afterwards.
        if (idle is not null)
        {
            Context = idle;
            _operationDriver = idle.Driver;
            _slot = slot;

            return;
        }

        var operationDriver = fileContext.Driver.TryCreateOperationDriver();

        // The source cannot isolate a cursor (a plain Stream has exactly one). Reads through it are
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

    private NativeOperationScope(NativeReadContext continuedContext, bool _)
    {
        Context = continuedContext;
        _operationDriver = null;
        _slot = null;
    }

    /// <summary>
    /// Continues <paramref name="operationContext" /> when it already reads
    /// <paramref name="file" />, and opens a new operation on <paramref name="file" /> otherwise.
    /// </summary>
    /// <remarks>
    /// Object navigation can cross files: an external link hands back a
    /// <c>NativeNamedReference</c> into the LINKED file, and a path walk or a <c>Children()</c>
    /// enumeration then continues through it. Those bytes are reachable only through that file's own
    /// driver - an operation belonging to another file does not merely have a stale cursor there, it
    /// reads an entirely different byte stream and yields a signature/checksum error or silent
    /// garbage. So matching the file is a correctness requirement, and the reason this is not simply
    /// "always open a new scope" is the other half: while the file DOES match, the whole walk has to
    /// stay on one driver instead of taking a fresh one per segment.
    /// <para>
    /// The continued case allocates nothing and its <see cref="Dispose" /> does nothing - the scope
    /// it continues still owns the driver and hands it back itself.
    /// </para>
    /// </remarks>
    public static NativeOperationScope ForFile(NativeFile? file, NativeReadContext operationContext)
    {
        if (file is null || ReferenceEquals(file, operationContext.File))
            return new NativeOperationScope(operationContext, false);

        return new NativeOperationScope(file.Context);
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
