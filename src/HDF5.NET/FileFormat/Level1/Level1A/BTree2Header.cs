using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HDF5.NET
{
    public class BTree2Header<T> : FileBlock where T : BTree2Record
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
            H5Utils.ValidateSignature(signature, BTree2Header<T>.Signature);

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
            this.RootNodeAddress = superblock.ReadOffset(reader);

            // root node record count
            this.RootNodeRecordCount = reader.ReadUInt16();

            // b-tree total record count
            this.BTreeTotalRecordCount = superblock.ReadLength(reader);

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
                    throw new FormatException($"Only version 0 instances of type {nameof(BTree2Header<T>)} are supported.");

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

        public BTree2Node<T>? RootNode
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
                        ? (BTree2Node<T>)new BTree2InternalNode<T>(this.Reader, _superblock, this, this.RootNodeRecordCount, this.Depth)
                        : (BTree2Node<T>)new BTree2LeafNode<T>(this.Reader, _superblock, this, this.RootNodeRecordCount);
                }
            }
        }

        internal BTree2NodeInfo[] NodeInfos { get; }

        internal byte MaxRecordCountSize { get; }

        #endregion

        #region Methods

        public IEnumerable<T> GetRecords()
        {
            var rootNode = this.RootNode;

            if (rootNode != null)
                return this.GetRecords(rootNode, this.Depth);
            else
                return new List<T>();
        }

        public IEnumerable<T> GetRecords(BTree2Node<T> node, ushort nodeLevel)
        {
            // This method could be rearranged to accept a BTree2NodePointer (instead of the root node).
            // In that case it would be possible to simplify the double check for internal/leaf node.

            // internal node
            var internalNode = node as BTree2InternalNode<T>;

            if (internalNode != null)
            {
                var records = node.Records
                    .Cast<T>()
                    .ToList();

                var nodePointers = internalNode.NodePointers;

                for (int i = 0; i < nodePointers.Length; i++)
                {
                    // there is one more node pointer than records
                    if (i < records.Count)
                        yield return records[i];

                    var nodePointer = nodePointers[i];
                    this.Reader.BaseStream.Seek((long)nodePointer.Address, SeekOrigin.Begin);
                    var childNodeLevel = (ushort)(nodeLevel - 1);

                    IEnumerable<T> childRecords;

                    // internal node
                    if (childNodeLevel > 0)
                    {
                        var childNode = new BTree2InternalNode<T>(this.Reader, _superblock, this, nodePointer.RecordCount, childNodeLevel);
                        childRecords = this.GetRecords(childNode, childNodeLevel);
                    }
                    // leaf node
                    else
                    {
                        var childNode = new BTree2LeafNode<T>(this.Reader, _superblock, this, nodePointer.RecordCount);
                        childRecords = childNode.Records;
                    }

                    foreach (var record in childRecords)
                    {
                        yield return record;
                    }
                }
            }
            // leaf node
            else
            {
                foreach (var record in node.Records)
                {
                    yield return record;
                }
            }

            // alterantive version to get tree dictionary:

            //if (rootNode != null)
            //{
            //    // root node
            //    nodeMap[nodeLevel] = new List<BTree2Node>() { rootNode };
            //    nodeLevel--;

            //    // internal nodes
            //    while (nodeLevel > 0)
            //    {
            //        var newInternalNodes = new List<BTree2Node>();

            //        foreach (BTree2InternalNode parentNode in nodeMap[nodeLevel + 1U])
            //        {
            //            foreach (var nodePointer in parentNode.NodePointers)
            //            {
            //                this.Reader.BaseStream.Seek((long)nodePointer.Address, SeekOrigin.Begin);
            //                var internalNode = new BTree2InternalNode(this.Reader, _superblock, this, nodePointer.RecordCount, nodeLevel);
            //                newInternalNodes.Add(internalNode);
            //            }
            //        }

            //        nodeMap[nodeLevel] = newInternalNodes;
            //        nodeLevel--;
            //    }

            //    // leaf nodes
            //    var newLeafNodes = new List<BTree2Node>();

            //    foreach (BTree2InternalNode parentNode in nodeMap[1])
            //    {
            //        foreach (var nodePointer in parentNode.NodePointers)
            //        {
            //            this.Reader.BaseStream.Seek((long)nodePointer.Address, SeekOrigin.Begin);
            //            var leafNode = new BTree2LeafNode(this.Reader, _superblock, this, nodePointer.RecordCount);
            //            newLeafNodes.Add(leafNode);
            //        }
            //    }

            //    nodeMap[nodeLevel] = newLeafNodes;
            //}

            //return nodeMap;
        }

        #endregion
    }
}
