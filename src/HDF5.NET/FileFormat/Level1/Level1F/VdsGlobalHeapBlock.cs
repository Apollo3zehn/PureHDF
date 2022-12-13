namespace HDF5.NET
{
    internal class VdsGlobalHeapBlock : FileBlock
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public VdsGlobalHeapBlock(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // entry count
            EntryCount = superblock.ReadLength(reader);

            // vds dataset entries
            VdsDatasetEntries = new VdsDatasetEntry[(int)EntryCount];

            for (ulong i = 0; i < EntryCount; i++)
            {
                VdsDatasetEntries[i] = new VdsDatasetEntry(reader);
            }

            // checksum
            Checksum = reader.ReadUInt32();
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
        public VdsDatasetEntry[] VdsDatasetEntries { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
