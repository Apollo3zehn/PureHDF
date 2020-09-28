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

        private H5File(H5BinaryReader reader, Superblock superblock, ObjectHeader objectHeader, string absoluteFilePath, bool deleteOnClose)
            : base(objectHeader)
        {
            this.Reader = reader;
            this.Superblock = superblock;
            this.Path = absoluteFilePath;
            _deleteOnClose = deleteOnClose;
        }

        #endregion

        #region Properties

        public string Path { get; } = ":memory:";

        internal H5BinaryReader Reader { get; }
        internal Superblock Superblock { get; }

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

            ObjectHeader objectHeader;
            var superblock01 = superblock as Superblock01;

            if (superblock01 != null)
            {
                var nullableObjectHeader = superblock01.RootGroupSymbolTableEntry.ObjectHeader;

                if (nullableObjectHeader == null)
                    throw new Exception("The root group object header is not allocated.");

                objectHeader = nullableObjectHeader;
            }
            else
            {
                var superblock23 = superblock as Superblock23;

                if (superblock23 != null)
                    objectHeader = superblock23.RootGroupObjectHeader;
                else
                    throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
            }

            return new H5File(reader, superblock, objectHeader, filePath, deleteOnClose);
        }

        public void Dispose()
        {
            H5Cache.Clear(this.Superblock);
            this.Reader.Dispose();

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
