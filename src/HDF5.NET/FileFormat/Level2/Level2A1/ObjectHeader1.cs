using System.Collections.Generic;

namespace HDF5.NET
{
    public class ObjectHeader1
    {
        #region Constructors

        public ObjectHeader1()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ushort HeaderMessagesCount { get; set; }
        public ushort ObjectReferenceCount { get; set; }
        public ushort ObjectHeaderSize { get; set; }
        public List<HeaderMessageType> HeaderMessageTypes { get; set; }
        public List<ushort> HeaderMessageDataSize { get; set; }
        public List<HeaderMessageFlags> HeaderMessageFlags { get; set; }
        public List<byte[]> HeaderMessageData { get; set; }

        #endregion
    }
}
