using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public delegate bool FoundDelegate<TUserData>(ulong address, out TUserData userData);

    public class BTree1Node<T> where T : struct, IBTree1Key
    {
        #region Fields

        private H5BinaryReader _reader;
        private Superblock _superblock;

        #endregion

        #region Constructors

        public BTree1Node(H5BinaryReader reader, Superblock superblock)
        {
            _reader = reader;
            _superblock = superblock;

            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree1Node<T>.Signature);

            this.NodeType = (BTree1NodeType)reader.ReadByte();
            this.NodeLevel = reader.ReadByte();
            this.EntriesUsed = reader.ReadUInt16();

            this.LeftSiblingAddress = superblock.ReadOffset(reader);
            this.RightSiblingAddress = superblock.ReadOffset(reader);

            this.Keys = new List<T>();
            this.ChildAddresses = new List<ulong>();

            switch (this.NodeType)
            {
                case BTree1NodeType.Group:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
                        this.Keys.Add((T)(object)new BTree1GroupKey(reader, superblock));
                        this.ChildAddresses.Add(superblock.ReadOffset(reader));
                    }

                    this.Keys.Add((T)(object)new BTree1GroupKey(reader, superblock));
                    
                    break;

                case BTree1NodeType.RawDataChunks:

                    for (int i = 0; i < this.EntriesUsed; i++)
                    {
#warning How to correctly handle dimensionality?
                        this.Keys.Add((T)(object)new BTree1RawDataChunksKey(reader, dimensionality: 1));
                        this.ChildAddresses.Add(superblock.ReadOffset(reader));
                    }

                    this.Keys.Add((T)(object)new BTree1RawDataChunksKey(reader, dimensionality: 1));

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
        public List<T> Keys { get; }
        public List<ulong> ChildAddresses { get; }

        public BTree1Node<T> LeftSibling
        {
            get
            {
                _reader.Seek((long)this.LeftSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_reader, _superblock);
            }
        }

        public BTree1Node<T> RightSibling
        {
            get
            {
                _reader.Seek((long)this.RightSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public bool TryFindUserData<TUserData>([NotNullWhen(returnValue: true)] out TUserData userData,
                                       Func<T, T, int> compare3,
                                       FoundDelegate<TUserData> found)
            where TUserData : struct
        {
            userData = default;

            // H5B.c (H5B_find)

            /*
             * Perform a binary search to locate the child which contains
             * the thing for which we're searching.
             */
            (var index, var cmp) = this.LocateRecord(compare3);

            /* Check if not found */
            if (cmp != 0)
                return false;

            /*
             * Follow the link to the subtree or to the data node.
             */
            var childAddress = this.ChildAddresses[(int)index];

            if (this.NodeLevel > 0)
            {
                _reader.Seek((long)childAddress, SeekOrigin.Begin);
                var subtree = new BTree1Node<T>(_reader, _superblock);

                if (subtree.TryFindUserData(out userData, compare3, found))
                    return true;
            }
            else
            {
                if (found(childAddress, out userData))
                    return true;
            }

            return false;
        }

        public Dictionary<uint, List<BTree1Node<T>>> GetTree()
        {
            var nodeMap = new Dictionary<uint, List<BTree1Node<T>>>();
            var nodeLevel = this.NodeLevel;

            nodeMap[nodeLevel] = new List<BTree1Node<T>>() { this };

            while (nodeLevel > 0)
            {
                var newNodes = new List<BTree1Node<T>>();

                foreach (var parentNode in nodeMap[nodeLevel])
                {
                    foreach (var address in parentNode.ChildAddresses)
                    {
                        _reader.Seek((long)address, SeekOrigin.Begin);
                        newNodes.Add(new BTree1Node<T>(_reader, _superblock));
                    }
                }

                nodeLevel--;
                nodeMap[nodeLevel] = newNodes;
            }

            return nodeMap;
        }

        private (uint index, int cmp) LocateRecord(Func<T, T, int> compare3)
        {
            uint index = 0, low = 0, high;  /* Final, left & right key indices */
            int cmp = 1;                    /* Key comparison value */

            high = this.EntriesUsed;

            while (low < high && cmp != 0)
            {
                index = (low + high) / 2;

                /* compare */
                cmp = compare3(this.Keys[(int)index], this.Keys[(int)index + 1]);

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
