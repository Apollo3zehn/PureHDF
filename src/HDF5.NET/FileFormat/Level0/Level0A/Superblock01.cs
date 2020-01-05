namespace HDF5.NET
{
    public class Superblock01 : Superblock
    {
        #region Constructors

        public Superblock01()
        {
            //
        }

        #endregion

        #region Properties

        public char[] FormatSignature { get; set; }
        public byte SuperBlockVersion { get; set; }
        public byte FreeSpaceStorageVersion { get; set; }
        public byte RootGroupSymbolTableEntryVersion { get; set; }
        public byte SharedHeaderMessageFormatVersion { get; set; }
        public byte OffsetsSize { get; set; }
        public byte LengthsSize { get; set; }
        public ushort GroupLeafNodeK { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public uint FileConsistencyFlags { get; set; }
        public ushort IndexedStorageInternalNodeK { get; set; }
        public ulong BaseAddress { get; set; }
        public ulong FreeSpaceInfoAddress { get; set; }
        public ulong EndOfFileAddress { get; set; }
        public ulong DriverInformationBlockAddress { get; set; }
        public uint RootGroupSymbolTableEntry { get; set; }

        #endregion
    }
}
