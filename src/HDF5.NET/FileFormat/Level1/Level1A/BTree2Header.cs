using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HDF5.NET
{
    public class BTree2Header<T> : FileBlock where T : struct, IBTree2Record
    {
        #region Fields

        private Func<T> _decodeKey;

        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public BTree2Header(H5BinaryReader reader, Superblock superblock, Func<T> decodeKey) : base(reader)
        {
            _superblock = superblock;
            _decodeKey = decodeKey;

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
            this.RootNodePointer = new BTree2NodePointer()
            {
                Address = superblock.ReadOffset(reader),
                RecordCount = reader.ReadUInt16(),
                TotalRecordCount = superblock.ReadLength(reader)
            };

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
        public BTree2NodePointer RootNodePointer { get; set; }
        public uint Checksum { get; set; }

        public BTree2Node<T>? RootNode
        {
            get
            {
                if (_superblock.IsUndefinedAddress(this.RootNodePointer.Address))
                {
                    return null;
                }
                else
                {
                    this.Reader.Seek((long)this.RootNodePointer.Address, SeekOrigin.Begin);

                    return this.Depth != 0
                        ? (BTree2Node<T>)new BTree2InternalNode<T>(this.Reader, _superblock, this, this.RootNodePointer.RecordCount, this.Depth, _decodeKey)
                        : (BTree2Node<T>)new BTree2LeafNode<T>(this.Reader, this, this.RootNodePointer.RecordCount, _decodeKey);
                }
            }
        }

        internal BTree2NodeInfo[] NodeInfos { get; }

        internal byte MaxRecordCountSize { get; }

        internal T MinNativeRec { get; set; }

        internal T MaxNativeRec { get; set; }

        #endregion

        #region Methods

        public bool TryFindRecord(out T result, Func<T, int> compare)
        {
            /* H5B2.c (H5B2_find) */
            int cmp;
            uint index = 0;
            BTree2NodePosition curr_pos;
            result = default;

            /* Make copy of the root node pointer to start search with */
            var currentNodePointer = this.RootNodePointer;

            /* Check for empty tree */
            if (currentNodePointer.RecordCount == 0)
                return false;

#warning Optimizations missing.

            /* Current depth of the tree */
            var depth = this.Depth;

            /* Walk down B-tree to find record or leaf node where record is located */
            cmp = -1;
            curr_pos = BTree2NodePosition.Root;

            while (depth > 0)
            {
                this.Reader.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);
                var internalNode = new BTree2InternalNode<T>(this.Reader, _superblock, this, currentNodePointer.RecordCount, depth, _decodeKey);

                if (internalNode is null)
                    throw new Exception("Unable to load B-tree internal node.");

                /* Locate node pointer for child */
                (index, cmp) = this.LocateRecord(internalNode.Records, compare);

                if (cmp > 0)
                    index++;

                if (cmp != 0)
                {
                    /* Get node pointer for next node to search */
                    var nextNodePointer = internalNode.NodePointers[index];

                    /* Set the position of the next node */
                    if (curr_pos != BTree2NodePosition.Middle)
                    {
                        if (index == 0)
                        {
                            if (curr_pos == BTree2NodePosition.Left || curr_pos == BTree2NodePosition.Root)
                                curr_pos = BTree2NodePosition.Left;
                            else
                                curr_pos = BTree2NodePosition.Middle;
                        }
                        else if (index == internalNode.Records.Length)
                        {
                            if (curr_pos == BTree2NodePosition.Right || curr_pos == BTree2NodePosition.Root)
                                curr_pos = BTree2NodePosition.Right;
                            else
                                curr_pos = BTree2NodePosition.Middle;
                        }
                        else
                        {
                            curr_pos = BTree2NodePosition.Middle;
                        }
                    }

                    currentNodePointer = nextNodePointer;
                }
                else
                {
                    result = internalNode.Records[index];
                    return true;
                }

                /* Decrement depth we're at in B-tree */
                depth--;
            }

            {
                this.Reader.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);
                var leafNode = new BTree2LeafNode<T>(this.Reader, this, currentNodePointer.RecordCount, _decodeKey);

                /* Locate record */
                (index, cmp) = this.LocateRecord(leafNode.Records, compare);

                if (cmp == 0)
                {
                    result = leafNode.Records[index];
                    return true;

#warning Optimizations missing.
                }
            }

            return false;
        }

        public IEnumerable<T> EnumerateRecords()
        {
            var rootNode = this.RootNode;

            if (rootNode is not null)
                return this.EnumerateRecords(rootNode, this.Depth);
            else
                return new List<T>();
        }

        private IEnumerable<T> EnumerateRecords(BTree2Node<T> node, ushort nodeLevel)
        {
            // This method could be rearranged to accept a BTree2NodePointer (instead of the root node).
            // In that case it would be possible to simplify the double check for internal/leaf node.

            // internal node
            var internalNode = node as BTree2InternalNode<T>;

            if (internalNode is not null)
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
                    this.Reader.Seek((long)nodePointer.Address, SeekOrigin.Begin);
                    var childNodeLevel = (ushort)(nodeLevel - 1);

                    IEnumerable<T> childRecords;

                    // internal node
                    if (childNodeLevel > 0)
                    {
                        var childNode = new BTree2InternalNode<T>(this.Reader, _superblock, this, nodePointer.RecordCount, childNodeLevel, _decodeKey);
                        childRecords = this.EnumerateRecords(childNode, childNodeLevel);
                    }
                    // leaf node
                    else
                    {
                        var childNode = new BTree2LeafNode<T>(this.Reader, this, nodePointer.RecordCount, _decodeKey);
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
        }

        private (uint index, int cmp) LocateRecord(T[] records,
                                                   Func<T, int> compare)
        {
            // H5B2int.c (H5B2__locate_record)
            // Return: Comparison value for insertion location. Negative for record
            // to locate being less than value in *IDX.  Zero for record to
            // locate equal to value in *IDX.  Positive for record to locate
            // being greater than value in *IDX (which should only happen when
            // record to locate is greater than all records to search).
            uint low = 0, high;
            uint index = 0;
            int cmp = -1;

            high = (uint)records.Length;

            while (low < high && cmp != 0)
            {
                index = (low + high) / 2;
                cmp = compare(records[index]);

                if (cmp < 0)
                    high = index;
                else
                    low = index + 1;
            }

            return (index, cmp);
        }

        #endregion
    }
}
