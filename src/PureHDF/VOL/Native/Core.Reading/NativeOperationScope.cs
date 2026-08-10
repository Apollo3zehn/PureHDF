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
/// read concurrently, so the whole thing costs one context record plus one driver per read, and
/// nothing at all for a <c>Stream</c>-backed file.
/// </para>
/// </remarks>
internal readonly struct NativeOperationScope : IDisposable
{
    private readonly H5DriverBase? _operationDriver;

    public NativeOperationScope(NativeReadContext fileContext)
    {
        _operationDriver = fileContext.Driver.TryCreateOperationDriver();

        Context = _operationDriver is null
            ? fileContext
            : fileContext with { Driver = _operationDriver };
    }

    /// <summary>
    /// The context every step of the operation must use. Reaching for the file-level context from
    /// inside an operation is the bug this type exists to prevent.
    /// </summary>
    public NativeReadContext Context { get; }

    public void Dispose()
    {
        _operationDriver?.Dispose();
    }
}
