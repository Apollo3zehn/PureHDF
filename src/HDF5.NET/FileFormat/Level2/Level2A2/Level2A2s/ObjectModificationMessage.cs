using System;

namespace HDF5.NET
{
    internal class ObjectModificationMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectModificationMessage(H5BinaryReader reader) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // seconds after unix epoch
            SecondsAfterUnixEpoch = reader.ReadUInt32();
        }

        public ObjectModificationMessage(uint secondsAfterUnixEpoch) : base(default!)
        {
            // version
            Version = 1;

            // seconds after unix epoch
            SecondsAfterUnixEpoch = secondsAfterUnixEpoch;
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
