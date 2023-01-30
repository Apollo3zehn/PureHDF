namespace PureHDF
{
    internal class HyperslabSelectionInfo2 : HyperslabSelectionInfo
    {
        #region Constructors

        public HyperslabSelectionInfo2(H5BaseReader reader)
        {
            // flags
            Flags = reader.ReadByte();

            // length
            Length = reader.ReadUInt32();

            // rank
            Rank = reader.ReadUInt32();

            // start, stride, count, block
            Starts = new ulong[Rank];
            Strides = new ulong[Rank];
            Counts = new ulong[Rank];
            Blocks = new ulong[Rank];

            CompactDimensions = new ulong[Rank];

            for (int i = 0; i < Rank; i++)
            {
                Starts[i] = reader.ReadUInt64();
                Strides[i] = reader.ReadUInt64();
                Counts[i] = reader.ReadUInt64();
                Blocks[i] = reader.ReadUInt64();

                CompactDimensions[i] = Blocks[i] * Counts[i];
            }
        }

        #endregion

        #region Properties

        public byte Flags { get; set; }
        public uint Length { get; set; }
        public ulong[] Starts { get; set; }
        public ulong[] Strides { get; set; }
        public ulong[] Counts { get; set; }
        public ulong[] Blocks { get; set; }

        #endregion
    }
}
