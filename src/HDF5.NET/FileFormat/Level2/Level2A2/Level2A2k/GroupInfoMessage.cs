namespace HDF5.NET
{
    internal class GroupInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public GroupInfoMessage(H5BaseReader reader)
        {
            // version
            Version = reader.ReadByte();

            // flags
            Flags = (GroupInfoMessageFlags)reader.ReadByte();

            // maximum compact value and minimum dense value
            if (Flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues))
            {
                MaximumCompactValue = reader.ReadUInt16();
                MinimumDenseValue = reader.ReadUInt16();
            }

            // estimated entry count and estimated entry link name length
            if (Flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation))
            {
                EstimatedEntryCount = reader.ReadUInt16();
                EstimatedEntryLinkNameLength = reader.ReadUInt16();
            }
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(GroupInfoMessage)} are supported.");

                _version = value;
            }
        }

        public GroupInfoMessageFlags Flags { get; set; }
        public ushort MaximumCompactValue { get; set; }
        public ushort MinimumDenseValue { get; set; }
        public ushort EstimatedEntryCount { get; set; }
        public ushort EstimatedEntryLinkNameLength { get; set; }

        #endregion
    }
}
