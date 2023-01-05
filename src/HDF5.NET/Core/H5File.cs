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
                       NamedReference reference,
                       ObjectHeader header,
                       string absoluteFilePath,
                       bool deleteOnClose)
            : base(context, reference, header)
        {
            Path = absoluteFilePath;
            _deleteOnClose = deleteOnClose;
        }

        #endregion

        #region Methods

        internal static H5File OpenReadCore(string filePath, bool deleteOnClose = false)
        {
            return H5File.OpenCore(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose);
        }

        internal static H5File OpenCore(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, bool deleteOnClose = false)
        {
            var absoluteFilePath = System.IO.Path.GetFullPath(filePath);
            var stream = System.IO.File.Open(absoluteFilePath, fileMode, fileAccess, fileShare);

            return H5File.OpenCore(stream, absoluteFilePath, deleteOnClose);
        }

        private static H5File OpenCore(Stream stream, string absoluteFilePath, bool deleteOnClose = false)
        {
            if (!BitConverter.IsLittleEndian)
                throw new Exception("This library only works on little endian systems.");

            /* NOTES on async IO
             *
             * - Overview (1)
             * - It seems that (2) is not an issue anymore. I was unable to reproduce his findings with .NET 7.
             * - RandomAccess.ReadAsync(SafeFileHandle) also works when FileOptions.Asynchronous is not specified. 
             *   But then it runs on a different thread instead of being really asynchronous (3)
             *
             * 1) https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/#scatter-gather-io
             * 2) https://sergeyteplyakov.github.io/Blog/concurrency/2019/05/29/Shooting-Yourself-in-the-Foot-with-Concurrent-Use-of-FileStream-Position.html
             * 3) https://www.tabsoverspaces.com/233639-open-filestream-properly-for-asynchronous-reading-writing
             */

            var safeFileHandle = (stream as FileStream)?.SafeFileHandle;
            var reader = new H5BinaryReader(stream, safeFileHandle);

            // superblock
            var stepSize = 512;
            var signature = reader.ReadBytes(8);

            while (!H5File.ValidateSignature(signature, Superblock.FormatSignature))
            {
                reader.Seek(stepSize - 8, SeekOrigin.Current);

                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    throw new Exception("The file is not a valid HDF 5 file.");

                signature = reader.ReadBytes(8);
                stepSize *= 2;
            }

            var version = reader.ReadByte();

            var superblock = (Superblock)(version switch
            {
                0 => new Superblock01(reader, version),
                1 => new Superblock01(reader, version),
                2 => new Superblock23(reader, version),
                3 => new Superblock23(reader, version),
                _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
            });

            reader.BaseAddress = superblock.BaseAddress;

            ulong address;
            var superblock01 = superblock as Superblock01;

            if (superblock01 is not null)
            {
                address = superblock01.RootGroupSymbolTableEntry.HeaderAddress;
            }
            else
            {
                var superblock23 = superblock as Superblock23;

                if (superblock23 is not null)
                    address = superblock23.RootGroupObjectHeaderAddress;
                else
                    throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
            }


            reader.Seek((long)address, SeekOrigin.Begin);
            var context = new H5Context(reader, superblock);
            var header = ObjectHeader.Construct(context);

            var file = new H5File(context, default, header, absoluteFilePath, deleteOnClose);
            var reference = new NamedReference("/", address, file);
            file.Reference = reference;

            return file;
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
