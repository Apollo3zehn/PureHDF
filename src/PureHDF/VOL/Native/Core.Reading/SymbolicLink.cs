namespace PureHDF;

internal class SymbolicLink
{
    #region Constructors

    public SymbolicLink(string name, string linkValue, NativeGroup parent)
    {
        Name = name;
        Value = linkValue;
        Parent = parent;
    }

    public SymbolicLink(LinkMessage linkMessage, NativeGroup parent)
    {
        Name = linkMessage.LinkName;

        (Value, ObjectPath) = linkMessage.LinkInfo switch
        {
            SoftLinkInfo softLink => (softLink.Value, null),
            ExternalLinkInfo externalLink => (externalLink.FilePath, externalLink.FullObjectPath),
            _ => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
        };

        Parent = parent;
    }

    #endregion

    #region Properties

    public string Name { get; }

    public string Value { get; }

    public string? ObjectPath { get; }

    public NativeGroup Parent { get; }

    #endregion

    #region Methods

    public NativeNamedReference GetTarget(H5LinkAccess linkAccess, bool useAsync)
    {
        // this file
        if (string.IsNullOrWhiteSpace(ObjectPath))
        {
            try
            {
                var reference = Parent.InternalGet(Value, linkAccess);
                reference.Name = Name;
                return reference;
            }
            catch (Exception ex)
            {
                return new NativeNamedReference(Name, Superblock.UndefinedAddress)
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
                var absoluteFilePath = FilePathUtils.FindExternalFileForLinkAccess(Parent.File.FolderPath, Value, linkAccess) 
                    ?? throw new Exception($"Could not find file {Value}.");

                var externalFile = NativeCache
                    .GetNativeFile(Parent.Context.Driver, absoluteFilePath, useAsync: useAsync);

#if NETSTANDARD2_0
                    return externalFile.InternalGet(ObjectPath!, linkAccess);
#else
                return externalFile.InternalGet(ObjectPath, linkAccess);
#endif
            }
            catch (Exception ex)
            {
                return new NativeNamedReference(Name, Superblock.UndefinedAddress)
                {
                    Exception = ex
                };
            }
        }
    }

    #endregion
}