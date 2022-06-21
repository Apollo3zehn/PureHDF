using System;

namespace HDF5.NET
{
    internal class ObjectReferenceCountMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectReferenceCountMessage(H5BinaryReader reader) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // reference count
            ReferenceCount = reader.ReadUInt32();
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
                    throw new FormatException($"Only version 0 instances of type {nameof(ObjectReferenceCountMessage)} are supported.");

                _version = value;
            }
        }

        public uint ReferenceCount { get; set; }

        #endregion
    }
}
