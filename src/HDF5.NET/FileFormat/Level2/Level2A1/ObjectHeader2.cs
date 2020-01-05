using System.Collections.Generic;

namespace HDF5.NET
{
    public class ObjectHeader2
    {
        #region Constructors

        public ObjectHeader2()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public byte Flags { get; set; }
        public uint AccessTime { get; set; }
        public uint ModificationTime { get; set; }
        public uint ChangeTime { get; set; }
        public uint BirthTime { get; set; }
        public ushort CompactAttributesCount { get; set; }
        public ushort MinimumDenseAttributesCount { get; set; }
        public ulong SizeOfChunk0 { get; set; }
        public List<HeaderMessageType> HeaderMessageTypes { get; set; }
        public List<ushort> HeaderMessageDataSizes { get; set; }
        public List<HeaderMessageFlags> HeaderMessageFlags { get; set; }
        public List<ushort> HeaderMessageCreationOrder { get; set; }
        public List<byte[]> HeaderMessageData { get; set; }

        #endregion
    }
}
