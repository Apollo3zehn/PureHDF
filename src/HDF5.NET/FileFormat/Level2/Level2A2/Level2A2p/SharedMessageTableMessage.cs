using System;
using System.IO;

namespace HDF5.NET
{
    public class SharedMessageTableMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SharedMessageTableMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // shared object header message table address
            this.SharedObjectHeaderMessageTableAddress = superblock.ReadOffset(reader);

            // index count
            this.IndexCount = reader.ReadByte();
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
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(SharedMessageTableMessage)} are supported.");

                _version = value;
            }
        }

        public ulong SharedObjectHeaderMessageTableAddress { get; set; }
        public byte IndexCount { get; set; }

        public SharedObjectHeaderMessageTable SharedObjectHeaderMessageTable
        {
            get
            {
                this.Reader.Seek((long)this.SharedObjectHeaderMessageTableAddress, SeekOrigin.Begin);
                return new SharedObjectHeaderMessageTable(this.Reader);
            }
        }

        #endregion
    }
}
