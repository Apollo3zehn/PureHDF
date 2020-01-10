using System.IO;

namespace HDF5.NET
{
    public class BTree2Record07_0 : BTree2Record
    {
        #region Constructors

        public BTree2Record07_0(BinaryReader reader) : base(reader)
        {
            this.MessageLocation = (MessageLocation)reader.ReadByte();
            this.Hash = reader.ReadBytes(4);
            this.ReferenceCount = reader.ReadUInt32();
            this.HeapId = reader.ReadBytes(8);
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public byte[] Hash { get; set; }
        public uint ReferenceCount { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}
