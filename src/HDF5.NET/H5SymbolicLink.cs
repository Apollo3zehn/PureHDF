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

        public H5NamedReference GetTarget(H5LinkAccessPropertyList linkAccess)
        {
            try
            {
                // this file
                if (string.IsNullOrWhiteSpace(this.ObjectPath))
                {
                    return this.Parent.InternalGet(this.Value, linkAccess);
                }
                // external file
                else
                {
                    var absoluteFilePath = this.ConstructExternalFilePath(this.Value, linkAccess);
                    var objectPath = this.ObjectPath;
                    var externalFile = H5Cache.GetH5File(this.Parent.Context.Superblock, absoluteFilePath);

                    return externalFile.InternalGet(objectPath, linkAccess);
                }
            }
            catch
            {
                return default;
            }
        }

        private string ConstructExternalFilePath(string externalFilePath, H5LinkAccessPropertyList linkAccess)
        {
            // h5Fint.c (H5F_prefix_open_file)
            // reference: https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm

            if (!Uri.TryCreate(externalFilePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The external file path is not a valid URI.");

            // absolute
            if (uri.IsAbsoluteUri)
            {
                if (File.Exists(externalFilePath))
                    return externalFilePath;
            }
            // relative
            else
            {
                // prefixes
                var envVariable = Environment
                    .GetEnvironmentVariable("HDF5_EXT_PREFIX");

                if (envVariable != null)
                {
                    // cannot work in Windows
                    //var envPrefixes = envVariable.Split(":");

                    //foreach (var envPrefix in envPrefixes)
                    //{
                    //    var envResult = PathCombine(envPrefix, externalFilePath);

                    //    if (File.Exists(envResult))
                    //        return envResult;
                    //}

                    var envResult = PathCombine(envVariable, externalFilePath);

                    if (File.Exists(envResult))
                        return envResult;
                }

                // link access property list
                if (!string.IsNullOrWhiteSpace(linkAccess.ExternalFilePrefix))
                {
                    var propPrefix = linkAccess.ExternalFilePrefix;
                    var propResult = PathCombine(propPrefix, externalFilePath);

                    if (File.Exists(propResult))
                        return propResult;
                }

                // relative to this file
                var filePrefix = Path.GetDirectoryName(this.Parent.File.Path);
                var fileResult = PathCombine(filePrefix, externalFilePath);

                if (File.Exists(fileResult))
                    return fileResult;

                // relative to current directory
                var cdResult = Path.GetFullPath(externalFilePath);

                if (File.Exists(cdResult))
                    return cdResult;
            }

            throw new Exception($"Unable to open external file '{externalFilePath}'.");

            // helper
            string PathCombine(string prefix, string relativePath)
            {
                try
                {
                    return Path.Combine(prefix, relativePath);
                }
                catch (Exception)
                {
                    throw new Exception("Unable to construct absolute file path for external file.");
                }
            }
        }

        #endregion
    }
}
