using System.Text;

namespace HDF5.NET
{
    public class BTree2LeafNode<T> : BTree2Node<T> where T : BTree2Record
    {
        #region Constructors

        public BTree2LeafNode(H5BinaryReader reader, Superblock superblock, BTree2Header<T> header, ushort recordCount) 
            : base(reader, superblock, header, recordCount, BTree2LeafNode<T>.Signature)
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
