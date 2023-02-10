namespace PureHDF
{
    internal class VdsGlobalHeapBlock
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public VdsGlobalHeapBlock(H5BaseReader localReader, Superblock superblock)
        {
            // version
            Version = localReader.ReadByte();

            // entry count
            var entryCount = superblock.ReadLength(localReader);

            // vds dataset entries
            VdsDatasetEntries = new VdsDatasetEntry[(int)entryCount];

            for (ulong i = 0; i < entryCount; i++)
            {
                VdsDatasetEntries[i] = new VdsDatasetEntry(localReader);
            }

            // checksum
            Checksum = localReader.ReadUInt32();
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

        public VdsDatasetEntry[] VdsDatasetEntries { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
