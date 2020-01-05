using System;
using System.IO;

namespace HDF5.NET
{
    public class H5File
    {
        #region Constructors

        private H5File(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            this.Reader = new BinaryReader(File.Open(filePath, mode, fileAccess, fileShare));

            // superblock
            var signature = this.Reader.ReadBytes(8);
            this.ValidateSignature(signature);

            var version = this.Reader.ReadByte();

            this.Superblock = version switch
            {
                0 => new Superblock01(this.Reader, version),
                1 => new Superblock01(this.Reader, version),
                2 => new Superblock23(this.Reader, version),
                4 => new Superblock23(this.Reader, version),
                _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
            };

            this.Superblock.Validate();

            var a = ((Superblock01)this.Superblock).RootGroupSymbolTableEntry.ObjectHeader;
        }

        #endregion

        #region Properties

        public Superblock Superblock { get; set; }

        private BinaryReader Reader { get; set; }

        #endregion

        #region Methods

        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            return new H5File(filePath, mode, fileAccess, fileShare);
        }

        private void ValidateSignature(byte[] data)
        {
            if (data.Length == 8)
            {
                if (data[0] == 0x89 && data[1] == 0x48 && data[2] == 0x44 && data[3] == 0x46
                 && data[4] == 0x0d && data[5] == 0x0a && data[6] == 0x1a && data[7] == 0x0a)
                {
                    return;
                }
            }

            throw new Exception("The file is not a valid HDF 5 file.");
        }

        #endregion
    }
}
