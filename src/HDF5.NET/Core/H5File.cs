using System;
using System.IO;

namespace HDF5.NET
{
    partial class H5File
    {

#warning K-Values message https://forum.hdfgroup.org/t/problem-reading-version-1-8-hdf5-files-using-file-format-specification-document-clarification-needed/7568
        #region Fields

        private bool _deleteOnClose;
        private Func<IChunkCache>? _chunkCacheFactory;

        #endregion

        #region Constructors

        private H5File(H5Context context,
                       H5NamedReference reference,
                       ObjectHeader header,
                       string absoluteFilePath,
                       bool deleteOnClose)
            : base(context, reference, header)
        {
            this.Path = absoluteFilePath;
            _deleteOnClose = deleteOnClose;
        }

        #endregion

        #region Methods

        internal static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool deleteOnClose)
        {
            if (!BitConverter.IsLittleEndian)
                throw new Exception("This library only works on little endian systems.");

            var absoluteFilePath = System.IO.Path.GetFullPath(filePath);
            var stream = System.IO.File.Open(absoluteFilePath, mode, fileAccess, fileShare);

            return H5File.Open(stream, absoluteFilePath, deleteOnClose);
        }

        private static bool ValidateSignature(byte[] actual, byte[] expected)
        {
            if (actual.Length == expected.Length)
            {
                if (actual[0] == expected[0] && actual[1] == expected[1] && actual[2] == expected[2] && actual[3] == expected[3]
                 && actual[4] == expected[4] && actual[5] == expected[5] && actual[6] == expected[6] && actual[7] == expected[7])
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
