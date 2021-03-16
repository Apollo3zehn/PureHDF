using System;
using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    partial class H5SymbolicLink
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

        internal H5Group Parent { get; }

        #endregion
    }
}
