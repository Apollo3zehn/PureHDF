using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    public class H5SymbolicLink : H5Link
    {
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
                ExternalLinkInfo externalLink   => (externalLink.FilePath, externalLink.FullObjectPath),
                _                               => throw new Exception($"The link info type '{linkMessage.LinkInfo.GetType().Name}' is not supported.")
            };

            this.Parent = parent;
        }

        #endregion

        #region Properties

        public string LinkValue { get; }

        public string? FullObjectPath { get; }

        internal H5Group Parent { get; }

        #endregion

        #region Methods

        public H5Link GetTarget(H5LinkAccessPropertyList? linkAccess)
        {
            try
            {
                // this file
                if (string.IsNullOrWhiteSpace(this.FullObjectPath))
                {
                    return this.Parent.Get(this.LinkValue);
                }
                // external file
                else
                {
                    var absoluteFilePath = this.ConstructExternalFilePath(this.LinkValue, linkAccess);
                    var objectPath = this.FullObjectPath;
                    var externalFile = H5Cache.GetH5File(this.Parent.File.Superblock, absoluteFilePath);

                    return externalFile.Get(objectPath, linkAccess);
                }
            }
            catch (Exception ex)
            {
                return new H5UnresolvedLink(this.Name, ex);
            }
        }

        private string ConstructExternalFilePath(string externalFilePath, H5LinkAccessPropertyList? linkAccess)
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
                if (linkAccess.HasValue)
                {
                    var propPrefix = linkAccess.Value.ExternalFilePrefix;
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
