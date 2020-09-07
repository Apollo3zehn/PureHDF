using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HDF5.NET
{
    public class BTree1Node : FileBlock
    {
        #region Fields

        Superblock _superblock;

        #endregion

        #region Constructors

        public BTree1Node(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree1Node.Signature);

            this.NodeType = (BTree1NodeType)reader.ReadByte();
            this.NodeLevel = reader.ReadByte();
            this.EntriesUsed = reader.ReadUInt16();

            this.LeftSiblingAddress = superblock.ReadOffset(reader);
            this.RightSiblingAddress = superblock.ReadOffset(reader);

            this.Keys = new List<BTree1Key>();
            this.ChildAddresses = new List<ulong>();

            switch (this.NodeType)
            {
                case BTree1NodeType.Group:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
                        this.Keys.Add(new BTree1GroupKey(reader, superblock));
                        this.ChildAddresses.Add(superblock.ReadOffset(reader));
                    }

                    this.Keys.Add(new BTree1GroupKey(reader, superblock));

                    break;

                case BTree1NodeType.RawDataChunks:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
#warning How to correctly handle dimensionality?
                        this.Keys.Add(new BTree1RawDataChunksKey(reader, dimensionality: 1));
                        this.ChildAddresses.Add(superblock.ReadOffset(reader));
                    }

                    this.Keys.Add(new BTree1RawDataChunksKey(reader, dimensionality: 1));

                    break;

                default:
                    throw new NotSupportedException($"The node type '{this.NodeType}' is not supported.");
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

        public BTree1NodeType NodeType { get; }
        public byte NodeLevel { get;  }
        public ushort EntriesUsed { get; }
        public ulong LeftSiblingAddress { get; }
        public ulong RightSiblingAddress { get; }
        public List<BTree1Key> Keys { get; }
        public List<ulong> ChildAddresses { get; }

        public BTree1Node LeftSibling
        {
            get
            {
                this.Reader.Seek((long)this.LeftSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        public BTree1Node RightSibling
        {
            get
            {
                this.Reader.Seek((long)this.RightSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public BTree1Node GetChild(int index)
        {
            this.Reader.Seek((long)this.ChildAddresses[index], SeekOrigin.Begin);
            return new BTree1Node(this.Reader, _superblock);
        }

        public Dictionary<uint, List<BTree1Node>> GetTree()
        {
            var nodeMap = new Dictionary<uint, List<BTree1Node>>();
            var nodeLevel = this.NodeLevel;

            nodeMap[nodeLevel] = new List<BTree1Node>() { this };

            while (nodeLevel > 0)
            {
                var newNodes = new List<BTree1Node>();

                foreach (var parentNode in nodeMap[nodeLevel])
                {
                    foreach (var address in parentNode.ChildAddresses)
                    {
                        this.Reader.Seek((long)address, SeekOrigin.Begin);
                        newNodes.Add(new BTree1Node(this.Reader, _superblock));
                    }
                }

                nodeLevel--;
                nodeMap[nodeLevel] = newNodes;
            }

            return nodeMap;
        }

        public List<SymbolTableNode> GetSymbolTableNodes()
        {
            var nodeLevel = 0U;

            return this.GetTree()[nodeLevel].SelectMany(node =>
            {
                return node.ChildAddresses.Select(address =>
                {
                    this.Reader.Seek((long)address, SeekOrigin.Begin);
                    return new SymbolTableNode(this.Reader, _superblock);
                });
            }).ToList();
        }

        #endregion
    }
}
