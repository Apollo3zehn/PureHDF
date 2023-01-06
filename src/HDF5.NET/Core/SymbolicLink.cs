using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    internal class SymbolicLink
    {
        #region Constructors

        public SymbolicLink(string name, string linkValue, H5Group parent)
        {
            Name = name;
            Value = linkValue;
            Parent = parent;
        }

        public SymbolicLink(LinkMessage linkMessage, H5Group parent)
        {
            Name = linkMessage.LinkName;

            (Value, ObjectPath) = linkMessage.LinkInfo switch
            {
                SoftLinkInfo softLink           => (softLink.Value, null),
                ExternalLinkInfo externalLink   => (externalLink.FilePath, externalLink.FullObjectPath),
                _                               => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
            };

            Parent = parent;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public string Value { get; }

        public string? ObjectPath { get; }

        public H5Group Parent { get; }

        #endregion

        #region Methods

        public NamedReference GetTarget(H5LinkAccess linkAccess, bool useAsync)
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
                    return new NamedReference(Name, Superblock.UndefinedAddress)
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
                    var absoluteFilePath = H5Utils.ConstructExternalFilePath(Parent.File, Value, linkAccess);
                    var objectPath = ObjectPath;
                    var externalFile = H5Cache.GetH5File(Parent.Context.Superblock, absoluteFilePath, useAsync: useAsync);

                    return externalFile.InternalGet(objectPath, linkAccess);
                }
                catch (Exception ex)
                {
                    return new NamedReference(Name, Superblock.UndefinedAddress)
                    {
                        Exception = ex
                    };
                }
            }
        }

        #endregion
    }
}
