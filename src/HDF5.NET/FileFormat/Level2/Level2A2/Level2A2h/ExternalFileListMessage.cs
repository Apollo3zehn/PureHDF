using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class ExternalFileListMessage : Message
    {
        #region Constructors

        public ExternalFileListMessage(BinaryReader reader) : base(reader)
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
