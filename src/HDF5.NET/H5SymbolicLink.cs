using System;
using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    public class H5SymbolicLink : H5Link
    {
        #region Fields

        H5Link? _target;

        #endregion

        #region Constructors

        internal H5SymbolicLink(string name, string linkValue, H5Group parent) : base(name)
        {
            this.LinkValue = linkValue;
            this.Parent = parent;
        }

        internal H5SymbolicLink(LinkMessage linkMessage, H5Group parent) : base(linkMessage.LinkName)
        {
            (this.LinkValue, this.FullObjectPath) = linkMessage.LinkInfo switch
            {
                SoftLinkInfo softLink           => (softLink.Value, null),
                ExternalLinkInfo externalLink   => (externalLink.FileName, externalLink.FullObjectPath),
                _                               => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
            };

            this.Parent = parent;
        }

        #endregion

        #region Properties

        public string LinkValue { get; }

        public string? FullObjectPath { get; }

        public H5Link Target 
        {
            get
            {
                if (_target == null)
                    _target = this.GetTarget();

                return _target;
            }
        }

        internal H5Group Parent { get; }

        #endregion

        #region Methods

        private H5Link GetTarget()
        {
            if (this.FullObjectPath != null)
            {
                throw new Exception("External links are not yet supported.");
            }

            return this.Parent.Get(this.LinkValue);
        }

        #endregion
    }
}
