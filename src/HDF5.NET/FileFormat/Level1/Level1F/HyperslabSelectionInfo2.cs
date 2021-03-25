namespace HDF5.NET
{
    internal class HyperslabSelectionInfo2 : HyperslabSelectionInfo
    {
        #region Constructors

        public HyperslabSelectionInfo2(H5BinaryReader reader) : base(reader)
        {
            // flags
            this.Flags = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // length
            this.Length = reader.ReadUInt32();

            // rank
            this.Rank = reader.ReadUInt32();

            // start, stride, count, block
            this.Starts = new ulong[this.Rank];
            this.Strides = new ulong[this.Rank];
            this.Counts = new ulong[this.Rank];
            this.Blocks = new ulong[this.Rank];

            for (int i = 0; i < this.Rank; i++)
            {
                this.Starts[i] = reader.ReadUInt64();
                this.Strides[i] = reader.ReadUInt64();
                this.Counts[i] = reader.ReadUInt64();
                this.Blocks[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public byte Flags { get; set; }
        public uint Length { get; set; }
        public uint Rank { get; set; }
        public ulong[] Starts { get; set; }
        public ulong[] Strides { get; set; }
        public ulong[] Counts { get; set; }
        public ulong[] Blocks { get; set; }

        #endregion
    }
}
