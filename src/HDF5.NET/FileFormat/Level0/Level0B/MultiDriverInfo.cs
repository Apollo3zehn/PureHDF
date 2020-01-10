using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public class MultiDriverInfo : DriverInfo
    {
        #region Constructors

        public MultiDriverInfo(BinaryReader reader) : base(reader)
        {
            // member mapping
            this.MemberMapping1 = (MemberMapping)reader.ReadByte();
            this.MemberMapping2 = (MemberMapping)reader.ReadByte();
            this.MemberMapping3 = (MemberMapping)reader.ReadByte();
            this.MemberMapping4 = (MemberMapping)reader.ReadByte();
            this.MemberMapping5 = (MemberMapping)reader.ReadByte();
            this.MemberMapping6 = (MemberMapping)reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // member count
            var memberCount = new MemberMapping[] { this.MemberMapping1, this.MemberMapping2, this.MemberMapping3,
                                                    this.MemberMapping4, this.MemberMapping5, this.MemberMapping6 }.Distinct().Count();

            // member start and end addresses
            this.MemberFileStartAddresses = new List<ulong>(memberCount);
            this.MemberFileEndAddresses = new List<ulong>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                this.MemberFileStartAddresses[i] = reader.ReadUInt64();
                this.MemberFileEndAddresses[i] = reader.ReadUInt64();
            }

            // member names
            this.MemberNames = new List<string>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                this.MemberNames[i] = H5Utils.ReadNullTerminatedString(reader, pad: true);
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
