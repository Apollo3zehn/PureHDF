using Microsoft.Extensions.Logging;
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

        public BTree1Node(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree1Node.Signature);

            this.NodeType = (BTree1NodeType)reader.ReadByte();
            this.NodeLevel = reader.ReadByte();
            this.EntriesUsed = reader.ReadUInt16();

            this.LeftSiblingAddress = superblock.ReadOffset();
            this.RightSiblingAddress = superblock.ReadOffset();

            this.Keys = new List<BTree1Key>();
            this.ChildAddresses = new List<ulong>();

            switch (this.NodeType)
            {
                case BTree1NodeType.Group:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
                        this.Keys.Add(new BTree1GroupKey(reader, superblock));
                        this.ChildAddresses.Add(superblock.ReadOffset());
                    }

                    this.Keys.Add(new BTree1GroupKey(reader, superblock));

                    break;

                case BTree1NodeType.RawDataChunks:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
#warning How to correctly handle dimensionality?
                        this.Keys.Add(new BTree1RawDataChunksKey(reader, dimensionality: 1));
                        this.ChildAddresses.Add(superblock.ReadOffset());
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
                this.Reader.BaseStream.Seek((long)this.LeftSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        public BTree1Node RightSibling
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.RightSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public BTree1Node GetChild(int index)
        {
            this.Reader.BaseStream.Seek((long)this.ChildAddresses[index], SeekOrigin.Begin);
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
                        this.Reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
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
                    this.Reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
                    return new SymbolTableNode(this.Reader, _superblock);
                });
            }).ToList();
        }

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"BTree1Node");
            logger.LogInformation($"BTree1Node NodeType: {this.NodeType}");
            logger.LogInformation($"BTree1Node Level {this.NodeLevel}");

            // keys
            for (int i = 0; i <= this.EntriesUsed; i++)
            {
                logger.LogInformation($"BTree1Node Keys[{i}]");
                this.Keys[i].Print(logger);
            }

            // child nodes
            for (int i = 0; i < this.EntriesUsed; i++)
            {
                logger.LogInformation($"BTree1Node GetChildNode({i}) (Address: {this.ChildAddresses[i]})");

                if (this.NodeLevel == 0)
                {
                    switch (this.NodeType)
                    {
                        case BTree1NodeType.Group:

                            //var symbolTableNode = this.GetSymbolTableNode(i);
                            //symbolTableNode.Print(logger);

                            break;

                        case BTree1NodeType.RawDataChunks:
                             break;

                        default:
                            throw new NotSupportedException($"The version 1 B-tree node type '{this.NodeType}' is not supported.");
                    }
                }
                else
                {
                    logger.LogInformation($"BTree1Node ChildNode[{i}]");
                    this.GetChild(i).Print(logger);
                }
            }
        }

        #endregion
    }
}
