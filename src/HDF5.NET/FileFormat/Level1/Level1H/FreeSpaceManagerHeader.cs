namespace HDF5.NET
{
    public class FreeSpaceManagerHeader
    {
        #region Constructors

        public FreeSpaceManagerHeader()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ClientId ClientId { get; set; }
        public ulong TotalSpaceTracked { get; set; }
        public ulong TotalSectionsCount { get; set; }
        public ulong SerializedSectionsCount { get; set; }
        public ulong UnSerializedSectionsCount { get; set; }
        public byte SectionClassesCount { get; set; }
        public byte ShrinkPercent { get; set; }
        public byte ExpandPercent { get; set; }
        public ulong MinimumSectionSize { get; set; }
        public ulong SerializedSectionListAddress { get; set; }
        public ulong SerializedSectionListUsed { get; set; }
        public ulong SerializedSectionListAllocatedSize { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
