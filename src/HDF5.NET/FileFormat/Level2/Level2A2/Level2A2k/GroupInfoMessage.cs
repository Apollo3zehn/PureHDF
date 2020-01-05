using System.IO;

namespace HDF5.NET
{
    public class GroupInfoMessage : Message
    {
        #region Constructors

        public GroupInfoMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public GroupInfoMessageFlags Flags { get; set; }
        public ushort MaximumCompactValue { get; set; }
        public ushort MinimumDenseValue { get; set; }
        public ushort EstimatedEntryCount { get; set; }
        public ushort EstimatedEntryLinkNameLength { get; set; }

        #endregion
    }
}
