using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PureHDF.VOL.Native;

internal delegate bool FoundDelegate<T, TUserData>(ulong address, T leftNode, out TUserData userData);

// TODO: better use class here? Benchmark required
internal readonly record struct BTree1Node<T>(
    NativeReadContext Context,
    Func<T> DecodeKey,
    byte NodeLevel,
    ushort EntriesUsed,
    ulong LeftSiblingAddress,
    ulong RightSiblingAddress,
    T[] Keys,
    ulong[] ChildAddresses
) where T : struct, IBTree1Key
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

    public static BTree1Node<T> Decode(NativeReadContext context, Func<T> decodeKey)
    {
        var (driver, superblock) = context;

        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, BTree1Node<T>.Signature);

        var nodeType = (BTree1NodeType)driver.ReadByte();
        var nodeLevel = driver.ReadByte();
        var entriesUsed = driver.ReadUInt16();

        var leftSiblingAddress = superblock.ReadOffset(driver);
        var rightSiblingAddress = superblock.ReadOffset(driver);

        var keys = new T[entriesUsed + 1];
        var childAddresses = new ulong[entriesUsed];

        for (int i = 0; i < entriesUsed; i++)
        {
            keys[i] = decodeKey();
            childAddresses[i] = superblock.ReadOffset(driver);
        }

        keys[entriesUsed] = decodeKey();

        return new BTree1Node<T>(
            context, 
            decodeKey,
            nodeLevel,
            entriesUsed,
            leftSiblingAddress, 
            rightSiblingAddress,
            keys,
            childAddresses
        );
    }

    public readonly BTree1Node<T> LeftSibling
    {
        get
        {
            Context.Driver.Seek((long)LeftSiblingAddress, SeekOrigin.Begin);
            return BTree1Node<T>.Decode(Context, DecodeKey);
        }
    }

    public readonly BTree1Node<T> RightSibling
    {
        get
        {
            Context.Driver.Seek((long)RightSiblingAddress, SeekOrigin.Begin);
            return BTree1Node<T>.Decode(Context, DecodeKey);
        }
    }

    public readonly bool TryFindUserData<TUserData>(
        [NotNullWhen(returnValue: true)] out TUserData userData,
        Func<T, T, int> compare3,
        FoundDelegate<T, TUserData> found
    )
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
            Context.Driver.Seek((long)childAddress, SeekOrigin.Begin);
            var subtree = BTree1Node<T>.Decode(Context, DecodeKey);

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

    public readonly IEnumerable<BTree1Node<T>> EnumerateNodes()
    {
        return EnumerateNodes(this);
    }

    private readonly IEnumerable<BTree1Node<T>> EnumerateNodes(BTree1Node<T> node)
    {
        // internal node
        if (node.NodeLevel > 0)
        {
            foreach (var address in node.ChildAddresses)
            {
                Context.Driver.Seek((long)address, SeekOrigin.Begin);

                var childNode = BTree1Node<T>.Decode(Context, DecodeKey);

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

    private readonly (uint index, int cmp) LocateRecord(Func<T, T, int> compare3)
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
}