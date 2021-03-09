using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public delegate bool FoundDelegate<T, TUserData>(ulong address, T leftNode, out TUserData userData);

    public class BTree1Node<T> where T : struct, IBTree1Key
    {
        #region Fields

        private H5BinaryReader _reader;
        private Superblock _superblock;
        private Func<T> _decodeKey;

        #endregion

        #region Constructors

        public BTree1Node(H5BinaryReader reader, Superblock superblock, Func<T> decodeKey)
        {
            _reader = reader;
            _superblock = superblock;
            _decodeKey = decodeKey;

            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree1Node<T>.Signature);

            this.NodeType = (BTree1NodeType)reader.ReadByte();
            this.NodeLevel = reader.ReadByte();
            this.EntriesUsed = reader.ReadUInt16();

            this.LeftSiblingAddress = superblock.ReadOffset(reader);
            this.RightSiblingAddress = superblock.ReadOffset(reader);

            this.Keys = new T[this.EntriesUsed + 1];
            this.ChildAddresses = new ulong[this.EntriesUsed];

            for (int i = 0; i < this.EntriesUsed; i++)
            {
                this.Keys[i] = decodeKey();
                this.ChildAddresses[i] = superblock.ReadOffset(reader);
            }

            this.Keys[this.EntriesUsed] = decodeKey();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

        public BTree1NodeType NodeType { get; }
        public byte NodeLevel { get;  }
        public ushort EntriesUsed { get; }
        public ulong LeftSiblingAddress { get; }
        public ulong RightSiblingAddress { get; }
        public T[] Keys { get; }
        public ulong[] ChildAddresses { get; }

        public BTree1Node<T> LeftSibling
        {
            get
            {
                _reader.Seek((long)this.LeftSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_reader, _superblock, _decodeKey);
            }
        }

        public BTree1Node<T> RightSibling
        {
            get
            {
                _reader.Seek((long)this.RightSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_reader, _superblock, _decodeKey);
            }
        }

        #endregion

        #region Methods

        public bool TryFindUserData<TUserData>([NotNullWhen(returnValue: true)] out TUserData userData,
                                               Func<T, T, int> compare3,
                                               FoundDelegate<T, TUserData> found)
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
            var key = this.Keys[index];

            if (this.NodeLevel > 0)
            {
                _reader.Seek((long)childAddress, SeekOrigin.Begin);
                var subtree = new BTree1Node<T>(_reader, _superblock, _decodeKey);

                if (subtree.TryFindUserData(out userData, compare3, found))
                    return true;
            }
            else
            {
                if (found(childAddress, key, out userData))
                    return true;
            }

            return false;
        }

        public IEnumerable<BTree1Node<T>> EnumerateNodes()
        {
            return this.EnumerateNodes(this);
        }

        private IEnumerable<BTree1Node<T>> EnumerateNodes(BTree1Node<T> node)
        {
            // internal node
            if (node.NodeLevel > 0)
            {
                foreach (var address in node.ChildAddresses)
                {
                    _reader.Seek((long)address, SeekOrigin.Begin);

                    var childNode = new BTree1Node<T>(_reader, _superblock, _decodeKey);

                    // internal node
                    if ((node.NodeLevel - 1) > 0)
                    {
                        var internalNodes = this.EnumerateNodes(childNode);

                        foreach (var internalNode in internalNodes)
                        {
                            yield return internalNode;
                        }
                    }
                    // leaf node
                    else
                    {
                        yield return childNode;
                    }
                }
            }
            // leaf node
            else
            {
                yield return node;
            }
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
