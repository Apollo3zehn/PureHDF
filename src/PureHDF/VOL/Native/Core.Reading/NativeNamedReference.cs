namespace PureHDF.VOL.Native;

internal struct NativeNamedReference
{
    #region Constructors

    public NativeNamedReference(string name, ulong value, NativeFile file)
    {
        Name = name;
        Value = value;
        File = file;
        ScratchPad = null;
        Exception = null;
    }

    public NativeNamedReference(string name, ulong value)
    {
        Name = name;
        Value = value;
        File = null;
        ScratchPad = null;
        Exception = null;
    }

    #endregion

    #region Properties

    public string Name { get; set; }

    public ulong Value { get; }

    public NativeFile? File { get; }

    public ObjectHeaderScratchPad? ScratchPad { get; set; }

    public Exception? Exception { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Resolves this reference into an object, reading its header through
    /// <paramref name="operationContext" />.
    /// </summary>
    /// <param name="operationContext">
    /// The context of the navigation operation performing the dereference. It supplies the driver
    /// for the header read and must NOT end up stored on the returned object: that object outlives
    /// the operation, whose driver goes back to <c>NativeOperationSlot</c> and is then reused by an
    /// unrelated read. The returned object therefore receives the FILE-LEVEL context
    /// (<c>File.Context</c>) and opens a scope of its own for every later read or navigation call.
    /// <para>
    /// It is used only if it reads <see cref="File" />. An external link produces a reference into
    /// the LINKED file, and the operation resolving it belongs to the linking one - reading the
    /// header through it would read the wrong file entirely, so
    /// <c>NativeOperationScope.ForFile</c> switches to this reference's own file when they differ.
    /// </para>
    /// </param>
    public readonly NativeObject Dereference(NativeReadContext operationContext)
    {
        if (TryDereferenceWithoutReading(out var resolved))
            return resolved;

        using var scope = NativeOperationScope.ForFile(File, operationContext);

        scope.Context.Driver.SeekRelativeToBaseAddress((long)Value);

        // Blocks, because this serves the synchronous public surface. DereferenceAsync below is the
        // counterpart for the async one; both share everything except this line.
        var objectHeader = ObjectHeader.Construct(scope.Context).GetAwaiter().GetResult();

        return Materialize(objectHeader);
    }

    /// <inheritdoc cref="Dereference" />
    public readonly async ValueTask<NativeObject> DereferenceAsync(NativeReadContext operationContext)
    {
        if (TryDereferenceWithoutReading(out var resolved))
            return resolved;

        using var scope = NativeOperationScope.ForFile(File, operationContext);

        scope.Context.Driver.SeekRelativeToBaseAddress((long)Value);

        var objectHeader = await ObjectHeader.Construct(scope.Context).ConfigureAwait(false);

        return Materialize(objectHeader);
    }

    // The two cases that resolve without touching the driver at all: an unresolved link, and a group
    // whose scratch pad already carries what a NativeGroup needs.
    private readonly bool TryDereferenceWithoutReading(out NativeObject resolved)
    {
        if (File is null)
        {
            resolved = new NativeUnresolvedLink(this);
            return true;
        }

        if (ScratchPad is not null)
        {
            resolved = new NativeGroup(File.Context, this);
            return true;
        }

        resolved = default!;
        return false;
    }

    // File.Context, never the operation context: the returned object outlives this operation, whose
    // driver goes back to the slot - see the parameter documentation above.
    private readonly NativeObject Materialize(ObjectHeader objectHeader)
    {
        var fileContext = File!.Context;

        return objectHeader.ObjectType switch
        {
            ObjectType.Group => new NativeGroup(fileContext, this, objectHeader),
            ObjectType.Dataset => new NativeDataset(fileContext, this, objectHeader),
            ObjectType.CommitedDatatype => new NativeCommitedDatatype(fileContext, this, objectHeader),
            _ => throw new Exception("Unknown object type.")
        };
    }

    #endregion
}