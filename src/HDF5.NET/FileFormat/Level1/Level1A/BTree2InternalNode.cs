using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2InternalNode : BTree2Node
    {
        #region Constructors

        public BTree2InternalNode(BinaryReader reader, Superblock superblock, BTree2Header header, ushort recordCount, int nodeLevel)
            : base(reader, superblock, header, recordCount, BTree2LeafNode.Signature)
        {
            this.ChildAddresses = new List<ulong>();
            this.ChildRecordCounts = new List<ulong>();
            this.TotalRecordCounts = new List<ulong>();

            // H5B2cache.c (H5B2__cache_int_deserialize)
            for (int i = 0; i < recordCount + 1; i++)
            {
                // child address
                this.ChildAddresses.Add(superblock.ReadOffset());

                // child record count
                var childRecordCount = H5Utils.ReadUlong(reader, header.MaxRecordCountSize);
                this.ChildRecordCounts.Add(childRecordCount);

                // total child record count
                if (nodeLevel > 1)
                {
                    var totalChildRecordCount = H5Utils.ReadUlong(reader, header.NodeInfos[nodeLevel - 1].CumulatedTotalRecordCountSize);
                    this.TotalRecordCounts.Add(totalChildRecordCount);
                }
                else
                {
                    this.TotalRecordCounts.Add(childRecordCount);
                }
            }

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public List<ulong> ChildAddresses { get; }
        public List<ulong> ChildRecordCounts { get; }
        public List<ulong> TotalRecordCounts { get; }

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTIN");

        #endregion
    }
}
