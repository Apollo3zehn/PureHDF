using System;
using System.IO;

namespace HDF5.NET
{
    public class SharedMessage3 : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SharedMessage3(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // type
            this.Type = (SharedMessageLocation)reader.ReadByte();

            // address
            this.Location = superblock.ReadOffset(reader);
#warning the content could also be an 8 byte fractal heap ID
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
                if (value != 3)
                    throw new FormatException($"Only version 3 instances of type {nameof(SharedMessage3)} are supported.");

                _version = value;
            }
        }

        public SharedMessageLocation Type { get; set; }
        public ulong Location { get; set; }

        #endregion
    }
}
