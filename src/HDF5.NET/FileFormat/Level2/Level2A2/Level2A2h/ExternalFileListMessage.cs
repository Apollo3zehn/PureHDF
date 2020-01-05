using System.Collections.Generic;

namespace HDF5.NET
{
    public class ExternalFileListMessage
    {
        #region Constructors

        public ExternalFileListMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ushort AllocatedSlots { get; set; }
        public ushort UsedSlots { get; set; }
        public ulong HeapAddress { get; set; }
        public List<ExternalFileListSlot> SlotDefinitions { get; set; }

        #endregion
    }
}
