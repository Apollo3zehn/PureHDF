using System.IO;

namespace HDF5.NET
{
    public class BTree2Record05 : BTree2Record
    {
        #region Constructors

        public BTree2Record05(BinaryReader reader) : base(reader)
        {
            this.NameHash = reader.ReadBytes(4);
            this.Id = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public byte[] NameHash { get; set; }
        public byte[] Id { get; set; }

        #endregion
    }
}
