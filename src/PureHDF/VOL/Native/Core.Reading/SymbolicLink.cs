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

    public NativeNamedReference GetTarget(H5LinkAccess linkAccess)
    {
        // this file
        if (string.IsNullOrWhiteSpace(_objectPath))
        {
            try
            {
                var reference = _parent.InternalGet(_value, linkAccess);
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
                    .GetNativeFile(_parent.Context.Driver, absoluteFilePath);

#if NETSTANDARD2_0
                return externalFile.InternalGet(_objectPath!, linkAccess);
#else
                return externalFile.InternalGet(_objectPath, linkAccess);
#endif
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