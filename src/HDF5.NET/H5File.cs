using System;
using System.IO;

namespace HDF5.NET
{
    public class H5File : H5Group, IDisposable
    {
        #region Fields

        private bool _deleteOnClose;

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

        #region Properties

        public string Path { get; } = ":memory:";

        #endregion

        #region Methods

        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            return H5File.Open(filePath, mode, fileAccess, fileShare, deleteOnClose: false);
        }

        internal static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool deleteOnClose)
        {
            if (!BitConverter.IsLittleEndian)
                throw new Exception("This library only works on little endian systems.");

            var absoluteFilePath = System.IO.Path.GetFullPath(filePath);
            var reader = new H5BinaryReader(System.IO.File.Open(absoluteFilePath, mode, fileAccess, fileShare));

            // superblock
            int stepSize = 512;
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

            if (superblock01 != null)
            {
                address = superblock01.RootGroupSymbolTableEntry.HeaderAddress;
            }
            else
            {
                var superblock23 = superblock as Superblock23;

                if (superblock23 != null)
                    address = superblock23.RootGroupObjectHeaderAddress;
                else
                    throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
            }


            reader.Seek((long)address, SeekOrigin.Begin);
            var context = new H5Context(reader, superblock);
            var header = ObjectHeader.Construct(context);

            var file = new H5File(context, default, header, filePath, deleteOnClose);
            var reference = new H5NamedReference(file, "/", address);
            file.Reference = reference;

            return file;
        }

        public H5Object Get(H5Reference reference)
        {
            try
            {
                this.Context.Reader.Seek((long)reference.Value, SeekOrigin.Begin);
                var objectHeader = ObjectHeader.Construct(this.Context);
                var namedReference = new H5NamedReference(this, "", reference.Value);

                return namedReference.Dereference();
            }
            catch
            {
                throw new Exception($"Unable to dereference object with reference '{reference}'.");
            }
        }

        public void Dispose()
        {
            H5Cache.Clear(this.Context.Superblock);
            this.Context.Reader.Dispose();

            if (_deleteOnClose && System.IO.File.Exists(this.Path))
            {
                try
                {
                    System.IO.File.Delete(this.Path);
                }
                catch
                {
                    //
                }
            }    
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
