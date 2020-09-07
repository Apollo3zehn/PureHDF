using System;

namespace HDF5.NET
{
    public class GroupInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public GroupInfoMessage(H5BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // flags
            this.Flags = (GroupInfoMessageFlags)reader.ReadByte();

            // maximum compact value and minimum dense value
            if (this.Flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues))
            {
                this.MaximumCompactValue = reader.ReadUInt16();
                this.MinimumDenseValue = reader.ReadUInt16();
            }

            // estimated entry count and estimated entry link name length
            if (this.Flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation))
            {
                this.EstimatedEntryCount = reader.ReadUInt16();
                this.EstimatedEntryLinkNameLength = reader.ReadUInt16();
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
