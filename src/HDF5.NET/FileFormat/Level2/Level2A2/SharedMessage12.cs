using System;

namespace HDF5.NET
{
    public class SharedMessage12 : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SharedMessage12(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // type
            this.Type = reader.ReadByte();

            // reserved
            if (this.Version == 1)
                reader.ReadBytes(6);

            // address
            this.Address = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 0 and version 1 instances of type {nameof(SharedMessage12)} are supported.");

                _version = value;
            }
        }

        public byte Type { get; set; }
        public ulong Address { get; set; }

        #endregion
    }
}
