namespace PureHDF.VOL.Native;

internal class SymbolicLink
{
    #region Fields

    private readonly string _name;
    private readonly string _value;
    private readonly string? _objectPath;
    private readonly NativeFile _file;
    private readonly NativeGroup _parent;

    #endregion

    #region Constructors

    public SymbolicLink(string name, string linkValue, NativeGroup parent, NativeFile file)
    {
        _name = name;
        _value = linkValue;
        _parent = parent;
        _file = file;
    }

    public SymbolicLink(LinkMessage linkMessage, NativeGroup parent, NativeFile file)
    {
        _name = linkMessage.LinkName;

        (_value, _objectPath) = linkMessage.LinkInfo switch
        {
            SoftLinkInfo softLink => (softLink.Value, null),
            ExternalLinkInfo externalLink => (externalLink.FilePath, externalLink.FullObjectPath),
            _ => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
        };

        _parent = parent;
        _file = file;
    }

    #endregion

    #region Methods

    // CONCURRENCY: `operationContext` is the context of the navigation operation resolving this link.
    // The same-file branch continues on it, so a soft link costs no extra driver and the whole walk
    // stays on one cursor. The external-file branch deliberately does NOT use it: the target lives in
    // a different NativeFile whose driver reads a different byte stream, so it takes the
    // scope-creating InternalGet overload and runs on that file's own driver.
    //
    // Async because resolving a link is a path walk, and a path walk reads. Both callers
    // (NativeGroup.GetObjectReference and GetObjectReferencesForSymbolTableEntry) are themselves
    // async, so there is no synchronous counterpart worth keeping.
    public async ValueTask<NativeNamedReference> GetTarget(NativeReadContext operationContext, H5LinkAccess linkAccess)
    {
        // this file
        if (string.IsNullOrWhiteSpace(_objectPath))
        {
            try
            {
                var reference = await _parent.InternalGet(operationContext, _value, linkAccess).ConfigureAwait(false);
                reference.Name = _name;
                return reference;
            }
            catch (Exception ex)
            {
                return new NativeNamedReference(_name, Superblock.UndefinedAddress)
                {
                    Exception = ex
                };
            }
        }

        // external file
        else
        {
            try
            {
                var absoluteFilePath = FilePathUtils.FindExternalFileForLinkAccess(_file.FolderPath, _value, linkAccess)
                    ?? throw new Exception($"Could not find file {_value}.");

                var externalFile = NativeCache
                    .GetNativeFile(_parent.Context, absoluteFilePath);

                return await externalFile.InternalGet(_objectPath, linkAccess).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new NativeNamedReference(_name, Superblock.UndefinedAddress)
                {
                    Exception = ex
                };
            }
        }
    }

    #endregion
}