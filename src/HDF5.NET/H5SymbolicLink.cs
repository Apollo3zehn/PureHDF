using System;
using System.Diagnostics;
using System.IO;
using File = System.IO.File;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    internal class H5SymbolicLink
    {
        #region Constructors

        internal H5SymbolicLink(string name, string linkValue, H5Group parent)
        {
            this.Name = name;
            this.Value = linkValue;
            this.Parent = parent;
        }

        internal H5SymbolicLink(LinkMessage linkMessage, H5Group parent)
        {
            this.Name = linkMessage.LinkName;

            (this.Value, this.ObjectPath) = linkMessage.LinkInfo switch
            {
                SoftLinkInfo softLink           => (softLink.Value, null),
                ExternalLinkInfo externalLink   => (externalLink.FilePath, externalLink.FullObjectPath),
                _                               => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
            };

            this.Parent = parent;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public string Value { get; }

        public string? ObjectPath { get; }

        internal H5Group Parent { get; }

        #endregion

        #region Methods

        public H5NamedReference GetTarget(H5LinkAccess linkAccess)
        {
            // this file
            if (string.IsNullOrWhiteSpace(this.ObjectPath))
            {
                try
                {
                    var reference = this.Parent.InternalGet(this.Value, linkAccess);
                    reference.Name = this.Name;
                    return reference;
                }
                catch (Exception ex)
                {
                    return new H5NamedReference(this.Name, Superblock.UndefinedAddress)
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
                    var absoluteFilePath = H5Utils.ConstructExternalFilePath(this.Parent.File, this.Value, linkAccess);
                    var objectPath = this.ObjectPath;
                    var externalFile = H5Cache.GetH5File(this.Parent.Context.Superblock, absoluteFilePath);

                    return externalFile.InternalGet(objectPath, linkAccess);
                }
                catch (Exception ex)
                {
                    return new H5NamedReference(this.Name, Superblock.UndefinedAddress)
                    {
                        Exception = ex
                    };
                }
            }
        }

        #endregion
    }
}
