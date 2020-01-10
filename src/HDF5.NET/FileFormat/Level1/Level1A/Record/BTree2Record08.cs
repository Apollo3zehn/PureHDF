using System.IO;

namespace HDF5.NET
{
    public class BTree2Record08 : BTree2Record
    {
        #region Constructors

        public BTree2Record08(BinaryReader reader) : base(reader)
        {
            this.HeapId = reader.ReadBytes(8);
            this.MessageFlags = (HeaderMessageFlags)reader.ReadByte();
            this.CreationOrder = reader.ReadUInt32();
            this.NameHash = reader.ReadBytes(4);
        }

        #endregion

        #region Properties

        public byte[] HeapId { get; set; }
        public HeaderMessageFlags MessageFlags { get; set; }
        public uint CreationOrder { get; set; }
        public byte[] NameHash { get; set; }

        #endregion
    }
}
