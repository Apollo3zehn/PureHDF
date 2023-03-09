using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PureHDF
{
    internal delegate bool FoundDelegate<T, TUserData>(ulong address, T leftNode, out TUserData userData);

    internal class BTree1Node<T> where T : struct, IBTree1Key
    {
        #region Fields

        private H5Context _context;
        private readonly Func<T> _decodeKey;

        #endregion

        #region Constructors

        public BTree1Node(H5Context context, Func<T> decodeKey)
        {
            var (driver, superblock) = context;
            _context = context;

            _decodeKey = decodeKey;

            var signature = driver.ReadBytes(4);
            Utils.ValidateSignature(signature, BTree1Node<T>.Signature);

            NodeType = (BTree1NodeType)driver.ReadByte();
            NodeLevel = driver.ReadByte();
            EntriesUsed = driver.ReadUInt16();

            LeftSiblingAddress = superblock.ReadOffset(driver);
            RightSiblingAddress = superblock.ReadOffset(driver);

            Keys = new T[EntriesUsed + 1];
            ChildAddresses = new ulong[EntriesUsed];

            for (int i = 0; i < EntriesUsed; i++)
            {
                Keys[i] = decodeKey();
                ChildAddresses[i] = superblock.ReadOffset(driver);
            }

            Keys[EntriesUsed] = decodeKey();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

        public BTree1NodeType NodeType { get; }
        public byte NodeLevel { get; }
        public ushort EntriesUsed { get; }
        public ulong LeftSiblingAddress { get; }
        public ulong RightSiblingAddress { get; }
        public T[] Keys { get; }
        public ulong[] ChildAddresses { get; }

        public BTree1Node<T> LeftSibling
        {
            get
            {
                _context.Driver.Seek((long)LeftSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_context, _decodeKey);
            }
        }

        public BTree1Node<T> RightSibling
        {
            get
            {
                _context.Driver.Seek((long)RightSiblingAddress, SeekOrigin.Begin);
                return new BTree1Node<T>(_context, _decodeKey);
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
            (var index, var cmp) = LocateRecord(compare3);

            /* Check if not found */
            if (cmp != 0)
                return false;

            /*
             * Follow the link to the subtree or to the data node.
             */
            var childAddress = ChildAddresses[(int)index];
            var key = Keys[index];

            if (NodeLevel > 0)
            {
                _context.Driver.Seek((long)childAddress, SeekOrigin.Begin);
                var subtree = new BTree1Node<T>(_context, _decodeKey);

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
            return EnumerateNodes(this);
        }

        private IEnumerable<BTree1Node<T>> EnumerateNodes(BTree1Node<T> node)
        {
            // internal node
            if (node.NodeLevel > 0)
            {
                foreach (var address in node.ChildAddresses)
                {
                    _context.Driver.Seek((long)address, SeekOrigin.Begin);

                    var childNode = new BTree1Node<T>(_context, _decodeKey);

                    // internal node
                    if ((node.NodeLevel - 1) > 0)
                    {
                        var internalNodes = EnumerateNodes(childNode);

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

            high = EntriesUsed;

            while (low < high && cmp != 0)
            {
                index = (low + high) / 2;

                /* compare */
                cmp = compare3(Keys[(int)index], Keys[(int)index + 1]);

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
