using System;
using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class VdsGlobalHeapBlock : FileBlock
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public VdsGlobalHeapBlock(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            this.Version = reader.ReadUInt32();

            // entry count
            this.EntryCount = superblock.ReadLength(reader);

            // vds dataset entries
            this.VdsDatasetEntries = new List<VdsDatasetEntry>((int)this.EntryCount);

            for (ulong i = 0; i < this.EntryCount; i++)
            {
                this.VdsDatasetEntries.Add(new VdsDatasetEntry(reader));
            }

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(VdsGlobalHeapBlock)} are supported.");

                _version = value;
            }
        }

        public ulong EntryCount { get; set; }
        public List<VdsDatasetEntry> VdsDatasetEntries { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
