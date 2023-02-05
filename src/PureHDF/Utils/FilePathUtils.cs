using System.Runtime.InteropServices;

namespace PureHDF
{
    internal static class FilePathUtils
    {
        public static string? FindExternalFileForLinkAccess(string? thisFolderPath, string filePath, H5LinkAccess linkAccess)
        {
            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Lpublic.h#L1503-L1566
            // https://docs.hdfgroup.org/hdf5/v1_10/group___h5_l.html#title5

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The external link file path is not a valid URI.");

            // absolute
            if (uri.IsAbsoluteUri)
            {
                if (File.Exists(filePath))
                    return filePath;

                filePath = Path.GetFileName(filePath);
            }

            // relative

            // 1. environment variable
            var envVariable = Environment
                .GetEnvironmentVariable("HDF5_EXT_PREFIX");

            if (envVariable is not null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var envResult = Path.Combine(envVariable, filePath);

                    if (File.Exists(envResult))
                        return envResult;
                }
                else
                {
                    var envPrefixes = envVariable.Split(":");

                    foreach (var envPrefix in envPrefixes)
                    {
                        var envResult = Path.Combine(envPrefix, filePath);

                        if (File.Exists(envResult))
                            return envResult;
                    }
                }
            }

            // 2. link access property list
            if (linkAccess.ExternalLinkPrefix is not null)
            {
                var propPrefix = linkAccess.ExternalLinkPrefix;
                var propResult = Path.Combine(propPrefix, filePath);

                if (File.Exists(propResult))
                    return propResult;
            }

            // 3. this file path
            if (thisFolderPath is not null)
            {   
                var thisResult = Path.Combine(thisFolderPath, filePath);

                if (File.Exists(thisResult))
                    return thisResult;
            }

            // 4. relative path
            if (File.Exists(filePath))
                return filePath;

            return default;
        }

        public static string? FindExternalFileForDatasetAccess(string? thisFolderPath, string filePath, H5DatasetAccess datasetAccess)
        {
            const string ORIGIN_TOKEN = "${ORIGIN}";

            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L7084-L7116
            // https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11
            // https://github.com/HDFGroup/hdf5/issues/1759

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The virtual dataset file path is not a valid URI.");

            // absolute
            if (uri.IsAbsoluteUri)
            {
                if (File.Exists(filePath))
                    return filePath;

                else
                    return default;
            }

            // relative

            // 1. environment variable
            var envVariable = Environment
                .GetEnvironmentVariable("HDF5_EXTFILE_PREFIX");

            if (envVariable is not null)
            {
                if (envVariable.StartsWith(ORIGIN_TOKEN) && thisFolderPath is not null)
                    envVariable = Path.Combine(
                        thisFolderPath, 
                        envVariable[ORIGIN_TOKEN.Length..]);

                var envResult = Path.Combine(envVariable, filePath);

                if (File.Exists(envResult))
                    return envResult;
            }

            // 2. dataset access property list
            if (datasetAccess.ExternalFilePrefix is not null)
            {
                var propPrefix = datasetAccess.ExternalFilePrefix;

                if (propPrefix.StartsWith(ORIGIN_TOKEN) && thisFolderPath is not null)
                    propPrefix = Path.Combine(
                        thisFolderPath, 
                        propPrefix[ORIGIN_TOKEN.Length..]);

                var propResult = Path.Combine(propPrefix, filePath);

                if (File.Exists(propResult))
                    return propResult;
            }

            // 3. relative path
            if (File.Exists(filePath))
                return filePath;

            return default;
        }

        public static string? FindVirtualFile(string? thisFolderPath, string filePath, H5DatasetAccess datasetAccess)
        {
            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Ppublic.h#L6607-L6670
            // https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The virtual dataset file path is not a valid URI.");

            // self
            if (filePath == ".")
            {
                return ".";
            }

            // absolute
            if (uri.IsAbsoluteUri)
            {
                if (File.Exists(filePath))
                    return filePath;

                filePath = Path.GetFileName(filePath);
            }

            // relative

            // 1. environment variable
            var envVariable = Environment
                .GetEnvironmentVariable("HDF5_VDS_PREFIX");

            if (envVariable is not null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var envResult = Path.Combine(envVariable, filePath);

                    if (File.Exists(envResult))
                        return envResult;
                }
                else
                {
                    var envPrefixes = envVariable.Split(":");

                    foreach (var envPrefix in envPrefixes)
                    {
                        var envResult = Path.Combine(envPrefix, filePath);

                        if (File.Exists(envResult))
                            return envResult;
                    }
                }
            }

            // 2. dataset access property list
            if (datasetAccess.VirtualFilePrefix is not null)
            {
                var propPrefix = datasetAccess.VirtualFilePrefix;
                var propResult = Path.Combine(propPrefix, filePath);

                if (File.Exists(propResult))
                    return propResult;
            }

            // 3. this file path
            if (thisFolderPath is not null)
            {
                var thisResult = Path.Combine(thisFolderPath, filePath);

                if (File.Exists(thisResult))
                    return thisResult;
            }

            // 4. relative path
            if (File.Exists(filePath))
                return filePath;

            return default;
        }
    }
}
