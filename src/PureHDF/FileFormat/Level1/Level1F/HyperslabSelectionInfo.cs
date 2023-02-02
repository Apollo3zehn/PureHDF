namespace PureHDF
{
    internal abstract class HyperslabSelectionInfo
    {
        public uint Rank { get; set; }
        public ulong[] CompactDimensions { get; set; } = default!;

        public static HyperslabSelectionInfo Create(H5BaseReader reader, uint version)
        {
            uint rank;

            switch (version)
            {
                case 1:
                    // reserved
                    _ = reader.ReadBytes(4);

                    // length
                    _ = reader.ReadUInt32();

                    // rank
                    rank = reader.ReadUInt32();

                    return new IrregularHyperslabSelectionInfo(reader, rank, encodeSize: 4);

                case 2:
                    // flags
                    _ = reader.ReadByte();

                    // length
                    _ = reader.ReadUInt32();

                    // rank
                    rank = reader.ReadUInt32();
                    
                    return new RegularHyperslabSelectionInfo(reader, rank, encodeSize: 8);

                case 3:
                    // flags
                    var flags = reader.ReadByte();

                    // encode size
                    var encodeSize = reader.ReadByte();

                    // rank
                    rank = reader.ReadUInt32();

                    if ((flags & 0x01) == 1)
                        return new RegularHyperslabSelectionInfo(reader, rank, encodeSize);
                    else
                        return new IrregularHyperslabSelectionInfo(reader, rank, encodeSize);

                default:
                    throw new NotSupportedException($"Only {nameof(H5S_SEL_HYPER)} of version 1, 2 or 3 are supported.");
            }
        }

        protected static ulong ReadEncodedValue(H5BaseReader reader, byte encodeSize)
        {
            return encodeSize switch
            {
                2 => reader.ReadUInt16(),
                4 => reader.ReadUInt32(),
                8 => reader.ReadUInt64(),
                _ => throw new Exception($"Invalid encode size {encodeSize}.")
            };
        }
    }
}
