using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class FreeSpaceSectionList : FileBlock
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public FreeSpaceSectionList(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FreeSpaceSectionList.Signature);

            // version
            this.Version = reader.ReadByte();

            // free space manager header address
            this.FreeSpaceManagerHeaderAddress = superblock.ReadOffset(reader);

#warning implement everything

            // checksum
            this.Checksum = reader.ReadUInt32();
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
        public List<ulong> SectionRecordsCount { get; set; }
        public List<ulong> FreeSpaceSectionSize { get; set; }
        public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
        public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
        public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>

        public uint Checksum { get; set; }

        public FreeSpaceManagerHeader FreeSpaceManagerHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.FreeSpaceManagerHeaderAddress, SeekOrigin.Begin);
                return new FreeSpaceManagerHeader(this.Reader, _superblock);
            }
        }

        #endregion
    }
}
