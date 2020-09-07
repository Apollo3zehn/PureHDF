using System.IO;

namespace HDF5.NET
{
    public class BTree2Record06 : BTree2Record
    {
        #region Constructors

        public BTree2Record06(BinaryReader reader) : base(reader)
        {
            this.CreationOrder = reader.ReadUInt64();
            this.HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public ulong CreationOrder { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}
