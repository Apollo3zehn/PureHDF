using System;
using System.Text;

namespace HDF5.NET
{
    internal class BTree2LeafNode<T> : BTree2Node<T> where T : struct, IBTree2Record
    {
        #region Constructors

        public BTree2LeafNode(H5BinaryReader reader, BTree2Header<T> header, ushort recordCount, Func<T> decodeKey) 
            : base(reader, header, recordCount, BTree2LeafNode<T>.Signature, decodeKey)
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
