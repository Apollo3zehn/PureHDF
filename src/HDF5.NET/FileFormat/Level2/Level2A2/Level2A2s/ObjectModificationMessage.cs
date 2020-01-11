using System;
using System.IO;

namespace HDF5.NET
{
    public class ObjectModificationMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectModificationMessage(BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // seconds after unix epoch
            this.SecondsAfterUnixEpoch = reader.ReadUInt32();
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
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(ObjectModificationMessage)} are supported.");

                _version = value;
            }
        }

        public uint SecondsAfterUnixEpoch { get; set; }

        #endregion
    }
}
