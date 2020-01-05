namespace HDF5.NET
{
    public class Superblock23 : Superblock
    {
        #region Constructors

        public Superblock23()
        {
            //
        }

        #endregion

        #region Properties

        public char[] FormatSignature { get; set; }
        public byte SuperBlockVersion { get; set; }
        public byte OffsetsSize { get; set; }
        public byte LengthsSize { get; set; }
        public byte FileConsistencyFlags { get; set; }
        public ulong BaseAddress { get; set; }
        public ulong SuperblockExtensionAddress { get; set; }
        public ulong EndOfFileAddress { get; set; }
        public ulong RootGroupObjectHeaderAddress { get; set; }
        public uint SuperblockChecksum { get; set; }

        #endregion
    }
}
