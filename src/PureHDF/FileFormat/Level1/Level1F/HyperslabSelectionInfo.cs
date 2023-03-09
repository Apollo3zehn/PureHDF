namespace PureHDF
{
    internal abstract class HyperslabSelectionInfo
    {
        public uint Rank { get; set; }

        public static HyperslabSelectionInfo Create(H5DriverBase driver, uint version)
        {
            uint rank;

            switch (version)
            {
                case 1:
                    // reserved
                    _ = driver.ReadBytes(4);

                    // length
                    _ = driver.ReadUInt32();

                    // rank
                    rank = driver.ReadUInt32();

                    return new IrregularHyperslabSelectionInfo(driver, rank, encodeSize: 4);

                case 2:
                    // flags
                    _ = driver.ReadByte();

                    // length
                    _ = driver.ReadUInt32();

                    // rank
                    rank = driver.ReadUInt32();
                    
                    return new RegularHyperslabSelectionInfo(driver, rank, encodeSize: 8);

                case 3:
                    // flags
                    var flags = driver.ReadByte();

                    // encode size
                    var encodeSize = driver.ReadByte();

                    // rank
                    rank = driver.ReadUInt32();

                    if ((flags & 0x01) == 1)
                        return new RegularHyperslabSelectionInfo(driver, rank, encodeSize);
                    else
                        return new IrregularHyperslabSelectionInfo(driver, rank, encodeSize);

                default:
                    throw new NotSupportedException($"Only {nameof(H5S_SEL_HYPER)} of version 1, 2 or 3 are supported.");
            }
        }
    }
}
