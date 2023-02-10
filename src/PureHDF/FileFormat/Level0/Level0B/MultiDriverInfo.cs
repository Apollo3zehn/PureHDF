namespace PureHDF
{
    internal class MultiDriverInfo : DriverInfo
    {
        #region Constructors

        public MultiDriverInfo(H5BaseReader reader)
        {
            // member mapping
            MemberMapping1 = (MemberMapping)reader.ReadByte();
            MemberMapping2 = (MemberMapping)reader.ReadByte();
            MemberMapping3 = (MemberMapping)reader.ReadByte();
            MemberMapping4 = (MemberMapping)reader.ReadByte();
            MemberMapping5 = (MemberMapping)reader.ReadByte();
            MemberMapping6 = (MemberMapping)reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // member count
            var memberCount = new MemberMapping[] { MemberMapping1, MemberMapping2, MemberMapping3,
                                                    MemberMapping4, MemberMapping5, MemberMapping6 }.Distinct().Count();

            // member start and end addresses
            MemberFileStartAddresses = new List<ulong>(memberCount);
            MemberFileEndAddresses = new List<ulong>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                MemberFileStartAddresses[i] = reader.ReadUInt64();
                MemberFileEndAddresses[i] = reader.ReadUInt64();
            }

            // member names
            MemberNames = new List<string>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                MemberNames[i] = ReadUtils.ReadNullTerminatedString(reader, pad: true);
            }
        }

        #endregion

        #region Properties

        public MemberMapping MemberMapping1 { get; set; }
        public MemberMapping MemberMapping2 { get; set; }
        public MemberMapping MemberMapping3 { get; set; }
        public MemberMapping MemberMapping4 { get; set; }
        public MemberMapping MemberMapping5 { get; set; }
        public MemberMapping MemberMapping6 { get; set; }
        public List<ulong> MemberFileStartAddresses { get; set; }
        public List<ulong> MemberFileEndAddresses { get; set; }
        public List<string> MemberNames { get; set; }

        #endregion
    }
}
