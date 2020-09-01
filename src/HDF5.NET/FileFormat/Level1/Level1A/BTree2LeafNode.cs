using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2LeafNode : BTree2Node
    {
        #region Constructors

        public BTree2LeafNode(BinaryReader reader, Superblock superblock, BTree2Header header, ushort recordCount) 
            : base(reader, superblock, header, recordCount, BTree2LeafNode.Signature)
        {
            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTLF");

        #endregion
    }
}
