using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree1Node : FileBlock
    {
        #region Fields

#warning Correct?
        Superblock _superblock;

        #endregion

        #region Constructors

        public BTree1Node(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            var signature = reader.ReadBytes(4);
            this.ValidateSignature(signature, BTree1Node.Signature);

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

                    break;

                case BTree1NodeType.RawDataChunks:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
#warning How to correctly handle dimensionality?
                        this.Keys.Add(new BTree1RawDataChunksKey(reader, dimensionality: 1));
                        this.ChildAddresses.Add(superblock.ReadOffset());
                    }

                    break;

                default:
                    throw new NotSupportedException($"The node type '{this.NodeType}' is not supported.");
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

        public BTree1NodeType NodeType { get; set; }
        public byte NodeLevel { get; set; }
        public ushort EntriesUsed { get; set; }
        public ulong LeftSiblingAddress { get; set; }
        public ulong RightSiblingAddress { get; set; }
        public List<BTree1Key> Keys { get; set; }
        public List<ulong> ChildAddresses { get; set; }

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

        public SymbolTableNode GetSymbolTableNode(int index)
        {
            this.Reader.BaseStream.Seek((long)this.ChildAddresses[index], SeekOrigin.Begin);
            return new SymbolTableNode(this.Reader, _superblock);
        }

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"BTree1Node");

            base.Print(logger);

            logger.LogInformation($"BTree1Node NodeType: {this.NodeType}");
            logger.LogInformation($"BTree1Node Level {this.NodeLevel}");

            if (this.NodeType == BTree1NodeType.Group)
            {
                for (int i = 0; i < this.EntriesUsed; i++)
                {
                    // key
                    logger.LogInformation($"BTree1Node BTree1GroupKey[{i}]");
                    var groupKey = (BTree1GroupKey)this.Keys[i];
                    groupKey.Print(logger);

                    // address
                    logger.LogInformation($"BTree1Node ChildAddress[{i}] = {this.ChildAddresses[i]}");

                    if (this.NodeLevel == 0)
                    {
                        logger.LogInformation($"BTree1Node ChildNode[{i}]");

                        var symbolTableNode = this.GetSymbolTableNode(i);
                        symbolTableNode.Print(logger);
                    }
                    else
                    {
                        logger.LogInformation($"BTree1 ChildNode[{i}]");
                        this.GetChild(i).Print(logger);
                    }
                }
            }
            else
            {
                logger.LogInformation($"BTree1Node Type = {this.NodeType} is not supported yet. Stopping.");
            }
        }

        #endregion
    }
}
