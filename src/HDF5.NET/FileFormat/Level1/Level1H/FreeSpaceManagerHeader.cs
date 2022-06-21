using System;
using System.Text;

namespace HDF5.NET
{
    internal class FreeSpaceManagerHeader : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FreeSpaceManagerHeader(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FreeSpaceManagerHeader.Signature);

            // version
            Version = reader.ReadByte();

            // client ID
            ClientId = (ClientId)reader.ReadByte();

            // total space tracked
            TotalSpaceTracked = superblock.ReadLength(reader);

            // total sections count
            TotalSectionsCount = superblock.ReadLength(reader);

            // serialized sections count
            SerializedSectionsCount = superblock.ReadLength(reader);

            // un-serialized sections count
            UnSerializedSectionsCount = superblock.ReadLength(reader);

            // section classes count
            SectionClassesCount = reader.ReadUInt16();

            // shrink percent
            ShrinkPercent = reader.ReadUInt16();

            // expand percent
            ExpandPercent = reader.ReadUInt16();

            // address space size
            AddressSpaceSize = reader.ReadUInt16();

            // maximum section size
            MaximumSectionSize = superblock.ReadLength(reader);

            // serialized section list address
            SerializedSectionListAddress = superblock.ReadOffset(reader);

            // serialized section list used
            SerializedSectionListUsed = superblock.ReadLength(reader);

            // serialized section list allocated size
            SerializedSectionListAllocatedSize = superblock.ReadLength(reader);

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSHD");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceManagerHeader)} are supported.");

                _version = value;
            }
        }

        public ClientId ClientId { get; set; }
        public ulong TotalSpaceTracked { get; set; }
        public ulong TotalSectionsCount { get; set; }
        public ulong SerializedSectionsCount { get; set; }
        public ulong UnSerializedSectionsCount { get; set; }
        public ushort SectionClassesCount { get; set; }
        public ushort ShrinkPercent { get; set; }
        public ushort ExpandPercent { get; set; }
        public ushort AddressSpaceSize { get; set; }
        public ulong MaximumSectionSize { get; set; }
        public ulong SerializedSectionListAddress { get; set; }
        public ulong SerializedSectionListUsed { get; set; }
        public ulong SerializedSectionListAllocatedSize { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
