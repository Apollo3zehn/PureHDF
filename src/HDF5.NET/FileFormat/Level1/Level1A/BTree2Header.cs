using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2Header : FileBlock
    {
        #region Fields

        private Superblock _superblock;
        private byte _version;

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

            // from H5B2hdr.c
            this.NodeInfos = new BTree2NodeInfo[this.Depth + 1];

            /* Initialize leaf node info */
            var fixedSizeOverhead = 4U + 1U + 1U + 4U; // signature, version, type, checksum
            var maxLeafRecordCount = (this.NodeSize - fixedSizeOverhead) / this.RecordSize;
            this.NodeInfos[0].MaxRecordCount = maxLeafRecordCount;
            this.NodeInfos[0].SplitRecordCount = (this.NodeInfos[0].MaxRecordCount * this.SplitPercent) / 100;
            this.NodeInfos[0].MergeRecordCount = (this.NodeInfos[0].MaxRecordCount * this.MergePercent) / 100;
            this.NodeInfos[0].CumulatedTotalRecordCount = this.NodeInfos[0].MaxRecordCount;
            this.NodeInfos[0].CumulatedTotalRecordCountSize = 0;

            /* Compute size to store # of records in each node */
            /* (uses leaf # of records because its the largest) */
            this.MaxRecordCountSize = (byte)H5Utils.FindMinByteCount(this.NodeInfos[0].MaxRecordCount); ;

            /* Initialize internal node info */
            if (this.Depth > 0)
            {
                for (int i = 1; i < this.Depth + 1; i++)
                {
                    var pointerSize = (uint)(superblock.OffsetsSize + this.MaxRecordCountSize + this.NodeInfos[i - 1].CumulatedTotalRecordCountSize);
                    var maxInternalRecordCount = (this.NodeSize - (fixedSizeOverhead + pointerSize)) / this.RecordSize + pointerSize;

                    this.NodeInfos[i].MaxRecordCount = maxInternalRecordCount;
                    this.NodeInfos[i].SplitRecordCount = (this.NodeInfos[i].MaxRecordCount * this.SplitPercent) / 100;
                    this.NodeInfos[i].MergeRecordCount = (this.NodeInfos[i].MaxRecordCount * this.MergePercent) / 100;
                    this.NodeInfos[i].CumulatedTotalRecordCount = 
                        (this.NodeInfos[i].MaxRecordCount + 1) * 
                         this.NodeInfos[i - 1].MaxRecordCount + this.NodeInfos[i].MaxRecordCount;
                    this.NodeInfos[i].CumulatedTotalRecordCountSize = (byte)H5Utils.FindMinByteCount(this.NodeInfos[i].CumulatedTotalRecordCount);
                }
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTHD");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(BTree2Header)} are supported.");

                _version = value;
            }
        }

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
                {
                    return null;
                }
                else
                {
                    this.Reader.BaseStream.Seek((long)this.RootNodeAddress, SeekOrigin.Begin);

                    return this.Depth != 0
                        ? (BTree2Node)new BTree2InternalNode(this.Reader, _superblock, this, this.RootNodeRecordCount, this.RootNodeRecordCount)
                        : (BTree2Node)new BTree2LeafNode(this.Reader, _superblock, this, this.RootNodeRecordCount);
                }
            }
        }

        internal BTree2NodeInfo[] NodeInfos { get; }

        internal byte MaxRecordCountSize { get; }

        #endregion

        #region Methods

        public Dictionary<uint, List<BTree2Node>> GetTree()
        {
            var nodeMap = new Dictionary<uint, List<BTree2Node>>();
            var nodeLevel = this.Depth;
            var rootNode = this.RootNode;

            if (rootNode != null)
            {
                nodeMap[nodeLevel] = new List<BTree2Node>() { rootNode };

                while (nodeLevel > 0)
                {
                    var newNodes = new List<BTree2Node>();

                    foreach (var parentNode in nodeMap[nodeLevel])
                    {
                        //foreach (var address in parentNode.ChildAddresses)
                        //{
                        //    this.Reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
                        //    newNodes.Add(new BTree1Node(this.Reader, _superblock));
                        //}
                    }

                    nodeLevel--;
                    nodeMap[nodeLevel] = newNodes;
                }
            }

            return nodeMap;
        }

        #endregion
    }
}
