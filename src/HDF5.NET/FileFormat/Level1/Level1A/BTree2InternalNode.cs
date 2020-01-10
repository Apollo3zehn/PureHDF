using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2InternalNode : BTree2Node
    {
        #region Constructors

        public BTree2InternalNode(BinaryReader reader, BTree2Type type, ushort recordSize, ushort rootNodeRecordCount) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree2InternalNode.Signature);

            // version
            this.Version = reader.ReadByte();

            // type
#warning "It should always be the same as the B-tree type in the header."
            this.Type = (BTree2Type)reader.ReadByte();

            if (this.Type != type)
                throw new FormatException($"The BTree2 internal node type ('{this.Type}') does not match the type defined in the header ('{type}').");

            // records
#warning why is recordSize necessary?
            this.Records = new List<BTree2Record>(rootNodeRecordCount);

#warning Finish implementation
            // child nodes
            //this.ChildNodes = new List<BTree2ChildNode>();

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTIN");

        public byte Version { get; set; }
        public BTree2Type Type { get; set; }
        public List<BTree2Record> Records { get; set; }
        //public List<BTree2ChildNode> ChildNodes { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
