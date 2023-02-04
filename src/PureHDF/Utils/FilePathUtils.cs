namespace PureHDF
{
    internal static class FilePathUtils
    {
        public static string? FindExternalFileForLinkAccess(string thisFilePath, string filePath, H5LinkAccess linkAccess)
        {
            // HDF5 1.10 -> H5Lpublic.h @ H5Lcreate_external()
            // https://docs.hdfgroup.org/hdf5/v1_10/group___h5_l.html#title5

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The external link file path is not a valid URI.");

            // self
            if (filePath == ".")
            {
                return thisFilePath;
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
                .GetEnvironmentVariable("HDF5_EXT_PREFIX");

            if (envVariable is not null)
            {
                // cannot work on Windows
                //var envPrefixes = envVariable.Split(":");

                //foreach (var envPrefix in envPrefixes)
                //{
                //    var envResult = PathCombine(envPrefix, externalFilePath);

                //    if (File.Exists(envResult))
                //        return envResult;
                //}

                var envResult = PathCombine(envVariable, filePath);

                if (File.Exists(envResult))
                    return envResult;
            }

            // 2. link access property list
            if (linkAccess.ExternalLinkPrefix is not null)
            {
                var propPrefix = linkAccess.ExternalLinkPrefix;
                var propResult = PathCombine(propPrefix, filePath);

                if (File.Exists(propResult))
                    return propResult;
            }

            // 3. this file path
            var thisResult = PathCombine(thisFilePath, filePath);

            if (File.Exists(thisResult))
                return thisResult;

            return default;
        }

        public static string? FindExternalFileForDatasetAccess(string thisFilePath, string filePath, H5DatasetAccess datasetAccess)
        {
            // HDF5 1.10 -> H5public.h @ H5Pset_efile_prefix()
            // https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11
            // https://github.com/HDFGroup/hdf5/issues/1759

            #error Needs to be reimplemented ... why is there HDF5_EXT_PREFIX and HDF5_EXTFILE_PREFIX ?

            return default;
        }

        public static string? FindVirtualFile(string thisFilePath, string filePath, H5DatasetAccess datasetAccess)
        {
            // HDF5 1.10 -> H5public.h @ H5Pset_virtual()
            // https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The virtual dataset file path is not a valid URI.");

            // self
            if (filePath == ".")
            {
                return thisFilePath;
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
                // cannot work on Windows
                //var envPrefixes = envVariable.Split(":");

                //foreach (var envPrefix in envPrefixes)
                //{
                //    var envResult = PathCombine(envPrefix, externalFilePath);

                //    if (File.Exists(envResult))
                //        return envResult;
                //}

                var envResult = PathCombine(envVariable, filePath);

                if (File.Exists(envResult))
                    return envResult;
            }

            // 2. dataset access property list
            if (datasetAccess.VirtualFilePrefix is not null)
            {
                var propPrefix = datasetAccess.VirtualFilePrefix;
                var propResult = PathCombine(propPrefix, filePath);

                if (File.Exists(propResult))
                    return propResult;
            }

            // 3. this file path
            var thisResult = PathCombine(thisFilePath, filePath);

            if (File.Exists(thisResult))
                return thisResult;

            return default;
        }

        static string PathCombine(string prefix, string relativePath)
        {
            try
            {
                return Path.Combine(prefix, relativePath);
            }
            catch
            {
                throw new Exception("Unable to construct absolute file path.");
            }
        }
    }
}
