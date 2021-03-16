using System;

namespace HDF5.NET
{
    public partial class H5SymbolicLink
    {
        #region Properties

        public string Name { get; }

        public string Value { get; }

        public string? ObjectPath { get; }

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
