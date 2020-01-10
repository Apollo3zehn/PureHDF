using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2Header : FileBlock
    {
        #region Fields

#warning is this OK?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public BTree2Header(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree2Header.Signature);

            // version
            this.Version = reader.ReadByte();

            // type
            this.Type = (BTree2Type)reader.ReadByte();

            // node size
            this.NodeSize = reader.ReadUInt32();

            // record size
            this.RecordSize = reader.ReadUInt16();

            // depth
            this.Depth = reader.ReadUInt16();

            // split percent
            this.SplitPercent = reader.ReadByte();

            // merge percent
            this.MergePercent = reader.ReadByte();

            // root node address
            this.RootNodeAddress = superblock.ReadOffset();

            // root node record count
            this.RootNodeRecordCount = reader.ReadUInt16();

            // b-tree total record count
            this.BTreeTotalRecordCount = superblock.ReadLength();

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTHD");

        public byte Version { get; set; }
        public BTree2Type Type { get; set; }
        public uint NodeSize { get; set; }
        public ushort RecordSize { get; set; }
        public ushort Depth { get; set; }
        public byte SplitPercent { get; set; }
        public byte MergePercent { get; set; }
        public ulong RootNodeAddress { get; set; }
        public ushort RootNodeRecordCount { get; set; }
        public ulong BTreeTotalRecordCount { get; set; }
        public uint Checksum { get; set; }

        public BTree2Node? RootNode
        {
            get
            {
                if (_superblock.IsUndefinedAddress(this.RootNodeAddress))
                    return null;
                else
                    return (this.Depth == 1 
                        ? (BTree2Node)new BTree2LeafNode(this.Reader, this.Type, this.RecordSize, this.RootNodeRecordCount) 
                        : new BTree2InternalNode(this.Reader, this.Type, this.RecordSize, this.RootNodeRecordCount));
#warning is 'depth==1' correct?
            }
        }

        #endregion
    }
}
