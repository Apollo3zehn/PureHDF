namespace HDF5.NET
{
    internal class ExternalLinkInfo : LinkInfo
    {
        #region Fields

        private byte _version;
        private byte _flags;

        #endregion

        #region Constructors

        public ExternalLinkInfo(H5BinaryReader reader)
        {
            // value length
            ValueLength = reader.ReadUInt16();

            // version and flags
            var data = reader.ReadByte();
            Version = (byte)((data & 0xF0) >> 4); // take only upper 4 bits
            Flags = (byte)((data & 0x0F) >> 0); // take only lower 4 bits

            // file name
            FilePath = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);

            // full object path
            FullObjectPath = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);
        }

        #endregion

        #region Properties

        public ushort ValueLength { get; set; }

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(ExternalLinkInfo)} are supported.");

                _version = value;
            }
        }

        public byte Flags
        {
            get
            {
                return _flags;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"The flags of an {nameof(FillValueMessage)} instance must be equal to zero.");

                _flags = value;
            }
        }

        public string FilePath { get; set; }
        public string FullObjectPath { get; set; }

        #endregion
    }
}
