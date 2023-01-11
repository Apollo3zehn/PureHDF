using System.Text;

namespace HDF5.NET
{
    internal class FixedArrayHeader
    {
        #region Fields

        private byte _version;
        private Superblock _superblock;

        #endregion

        #region Constructors

        public FixedArrayHeader(H5BinaryReader reader, Superblock superblock)
        {
            _superblock = superblock;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FixedArrayHeader.Signature);

            // version
            Version = reader.ReadByte();

            // client ID
            ClientID = (ClientID)reader.ReadByte();

            // entry size
            EntrySize = reader.ReadByte();

            // page bits
            PageBits = reader.ReadByte();

            // entries count
            EntriesCount = superblock.ReadLength(reader);

            // data block address
            DataBlockAddress = superblock.ReadOffset(reader);

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FAHD");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayHeader)} are supported.");

                _version = value;
            }
        }

        public ClientID ClientID { get; }

        public byte EntrySize { get; }

        public byte PageBits { get; }

        public ulong EntriesCount { get; }

        public ulong DataBlockAddress { get; }

        public uint Checksum { get; }

        #endregion
    }
}
