using System.Collections.Generic;

namespace HDF5.NET
{
    public class ObjectHeaderContinuationBlock
    {
        #region Constructors

        public ObjectHeaderContinuationBlock()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public List<HeaderMessageType> HeaderMessageTypes { get; set; }
        public List<ushort> HeaderMessageDataSizes { get; set; }
        public List<HeaderMessageFlags> HeaderMessageFlags { get; set; }
        public List<ushort> HeaderMessageCreationOrder { get; set; }
        public List<byte[]> HeaderMessageData { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
