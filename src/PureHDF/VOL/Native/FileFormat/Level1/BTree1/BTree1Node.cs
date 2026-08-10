using System.Collections.Concurrent;
using System.Text;

namespace PureHDF.VOL.Native;

// NOTE (async-first): the original shape was
//     bool FoundDelegate<T, TUserData>(ulong address, T leftNode, out TUserData userData)
// An `out` parameter cannot coexist with `async` (CS1988), so the result is returned as a tuple
// instead. Callbacks supplied by NativeGroup/H5D_Chunk123_BTree1 need to await decode work, which
// is why this had to change shape rather than merely gain a ValueTask return.
internal delegate ValueTask<(bool Success, TUserData UserData)> FoundDelegate<T, TUserData>(
    ulong address,
    T leftNode);

// TODO: better use class here? Benchmark required
internal readonly record struct BTree1Node<T>(
    NativeReadContext Context,
    Func<ValueTask<T>> DecodeKey,
    byte NodeLevel,
    ushort EntriesUsed,
    ulong LeftSiblingAddress,
    ulong RightSiblingAddress,
    T[] Keys,
    ulong[] ChildAddresses,
    ConcurrentDictionary<ulong, BTree1Node<T>> Cache
) where T : struct, IBTree1Key
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

    public static async ValueTask<BTree1Node<T>> Decode(NativeReadContext context, Func<ValueTask<T>> decodeKey)
    {
        var (driver, superblock) = context;

        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, BTree1Node<T>.Signature);

        var nodeType = (BTree1NodeType)(await driver.ReadByte().ConfigureAwait(false));
        var nodeLevel = await driver.ReadByte().ConfigureAwait(false);
        var entriesUsed = await driver.ReadUInt16().ConfigureAwait(false);

        var leftSiblingAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var rightSiblingAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        var keys = new T[entriesUsed + 1];
        var childAddresses = new ulong[entriesUsed];

        for (int i = 0; i < entriesUsed; i++)
        {
            keys[i] = await decodeKey().ConfigureAwait(false);
            childAddresses[i] = await superblock.ReadOffset(driver).ConfigureAwait(false);
        }

        keys[entriesUsed] = await decodeKey().ConfigureAwait(false);

        return new BTree1Node<T>(
            context,
            decodeKey,
            nodeLevel,
            entriesUsed,
            leftSiblingAddress,
            rightSiblingAddress,
            keys,
            childAddresses,
            Cache: new()
        );
    }

    // NOTE (async propagation): was a property; C# has no async property getters,
    // so this became a method with the same name. No callers exist in the repo today.
    public readonly async ValueTask<BTree1Node<T>> LeftSibling()
    {
        Context.Driver.SeekRelativeToBaseAddress((long)LeftSiblingAddress);
        return await BTree1Node<T>.Decode(Context, DecodeKey).ConfigureAwait(false);
    }

    // NOTE (async propagation): was a property; see LeftSibling.
    public readonly async ValueTask<BTree1Node<T>> RightSibling()
    {
        Context.Driver.SeekRelativeToBaseAddress((long)RightSiblingAddress);
        return await BTree1Node<T>.Decode(Context, DecodeKey).ConfigureAwait(false);
    }

    // NOTE (async propagation): `out TUserData userData` cannot coexist with `async`
    // (CS1988), so the out parameter became a tuple return. Callers outside this file
    // (H5D_Chunk123_BTree1.cs, NativeGroup.cs) need updating — see report.
    public readonly async ValueTask<(bool Success, TUserData UserData)> TryFindUserData<TUserData>(
        Func<T, T, ValueTask<int>> compare3,
        FoundDelegate<T, TUserData> found
    )
        where TUserData : struct
    {
        // H5B.c (H5B_find)

        /*
         * Perform a binary search to locate the child which contains
         * the thing for which we're searching.
         */
        (var index, var cmp) = await LocateRecord(compare3).ConfigureAwait(false);

        /* Check if not found */
        if (cmp != 0)
            return (false, default);

        /*
         * Follow the link to the subtree or to the data node.
         */
        var childAddress = ChildAddresses[(int)index];
        var key = Keys[index];

        if (NodeLevel > 0)
        {
            if (!Cache.TryGetValue(childAddress, out var subtree))
            {
                Context.Driver.SeekRelativeToBaseAddress((long)childAddress);
                subtree = await BTree1Node<T>.Decode(Context, DecodeKey).ConfigureAwait(false);
                subtree = Cache.GetOrAdd(childAddress, subtree);
            }

            var (success, userData) = await subtree.TryFindUserData(compare3, found).ConfigureAwait(false);

            if (success)
                return (true, userData);
        }
        else
        {
            var (found2, userData) = await found(childAddress, key).ConfigureAwait(false);

            if (found2)
                return (true, userData);
        }

        return (false, default);
    }

    // NOTE (async propagation): iterator that reads becomes IAsyncEnumerable<T> (rule 8).
    // Caller outside this file (NativeGroup.cs:EnumerateSymbolTableNodes) needs updating —
    // see report.
    public readonly IAsyncEnumerable<BTree1Node<T>> EnumerateNodes()
    {
        return EnumerateNodes(this);
    }

    private readonly async IAsyncEnumerable<BTree1Node<T>> EnumerateNodes(BTree1Node<T> node)
    {
        // internal node
        if (node.NodeLevel > 0)
        {
            foreach (var address in node.ChildAddresses)
            {
                Context.Driver.SeekRelativeToBaseAddress((long)address);

                var childNode = await BTree1Node<T>.Decode(Context, DecodeKey).ConfigureAwait(false);

                // internal node
                if ((node.NodeLevel - 1) > 0)
                {
                    var internalNodes = EnumerateNodes(childNode);

                    await foreach (var internalNode in internalNodes)
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

    private readonly async ValueTask<(uint index, int cmp)> LocateRecord(Func<T, T, ValueTask<int>> compare3)
    {
        uint index = 0, low = 0, high;  /* Final, left & right key indices */
        int cmp = 1;                    /* Key comparison value */

        high = EntriesUsed;

        while (low < high && cmp != 0)
        {
            index = (low + high) / 2;

            /* compare */
            cmp = await compare3(Keys[(int)index], Keys[(int)index + 1]).ConfigureAwait(false);

            if (cmp < 0)
                high = index;
            else
                low = index + 1;
        }

        return (index, cmp);
    }
}