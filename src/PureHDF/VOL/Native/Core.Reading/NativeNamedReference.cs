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

    public readonly NativeObject Dereference()
    {
        if (File is null)
        {
            return new NativeUnresolvedLink(this);
        }

        else if (ScratchPad is not null)
        {
            return new NativeGroup(File.Context, this);
        }

        else
        {
            var context = File.Context;
            context.Driver.SeekRelativeToBaseAddress((long)Value);

            // NOTE (async propagation): ObjectHeader.Construct is now async. This
            // method has many synchronous, non-async-aware callers (NativeGroup.cs
            // iterates/LINQ-projects over it) and cannot itself become async, so
            // the call is bridged here — see report.
            var objectHeader = ObjectHeader.Construct(context).GetAwaiter().GetResult();

            return objectHeader.ObjectType switch
            {
                ObjectType.Group => new NativeGroup(context, this, objectHeader),
                ObjectType.Dataset => new NativeDataset(context, this, objectHeader),
                ObjectType.CommitedDatatype => new NativeCommitedDatatype(context, this, objectHeader),
                _ => throw new Exception("Unknown object type.")
            };
        }
    }

    #endregion
}