using System.Text;

namespace HDF5.NET
{
    internal class FreeSpaceSectionList : FileBlock
    {
        #region Fields

// TODO: OK like this?
        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public FreeSpaceSectionList(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FreeSpaceSectionList.Signature);

            // version
            Version = reader.ReadByte();

            // free space manager header address
            FreeSpaceManagerHeaderAddress = superblock.ReadOffset(reader);

// TODO: implement everything

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSSE");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceSectionList)} are supported.");

                _version = value;
            }
        }

        public ulong FreeSpaceManagerHeaderAddress { get; set; }

        // TODO: implement everything
        // public List<ulong> SectionRecordsCount { get; set; }
        // public List<ulong> FreeSpaceSectionSize { get; set; }
        // public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
        // public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
        // public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>

        public uint Checksum { get; set; }

        public FreeSpaceManagerHeader FreeSpaceManagerHeader
        {
            get
            {
                Reader.Seek((long)FreeSpaceManagerHeaderAddress, SeekOrigin.Begin);
                return new FreeSpaceManagerHeader(Reader, _superblock);
            }
        }

        #endregion
    }
}
