using System.Text;

namespace HDF5.NET
{
    internal class BTree2InternalNode<T> : BTree2Node<T> where T : struct, IBTree2Record
    {
        #region Constructors

        public BTree2InternalNode(H5BinaryReader reader, Superblock superblock, BTree2Header<T> header, ushort recordCount, int nodeLevel, Func<T> decodeKey)
            : base(reader, header, recordCount, BTree2InternalNode<T>.Signature, decodeKey)
        {
            NodePointers = new BTree2NodePointer[recordCount + 1];

            // H5B2cache.c (H5B2__cache_int_deserialize)
            for (int i = 0; i < recordCount + 1; i++)
            {
                // address
                NodePointers[i].Address = superblock.ReadOffset(reader);

                // record count
                var childRecordCount = H5Utils.ReadUlong(reader, header.MaxRecordCountSize);
                NodePointers[i].RecordCount = (ushort)childRecordCount;

                // total record count
                if (nodeLevel > 1)
                {
                    var totalChildRecordCount = H5Utils.ReadUlong(reader, header.NodeInfos[nodeLevel - 1].CumulatedTotalRecordCountSize);
                    NodePointers[i].TotalRecordCount = totalChildRecordCount;
                }
                else
                {
                    NodePointers[i].TotalRecordCount = childRecordCount;
                }
            }

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTIN");

        public BTree2NodePointer[] NodePointers { get; }

        #endregion
    }
}
