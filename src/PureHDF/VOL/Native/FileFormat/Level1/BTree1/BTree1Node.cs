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

// CONCURRENCY / CACHING: holds no NativeReadContext, so that a decoded tree can be cached per file
// (see NativeCache.GetStructure) and shared by concurrent navigation operations. The context and the
// key decoder are passed per call instead, which is also what makes the child-node cache below sound:
// a cached node is immutable and context-free, so handing the same instance to two operations reading
// through two different drivers is correct.
//
// A class rather than the former `readonly record struct` - it is what the type's own long-standing
// TODO asked for, and it is required now that instances are cached: a struct would be boxed and
// copied out on every cache hit.
internal sealed record class BTree1Node<T>(
    byte NodeLevel,
    ushort EntriesUsed,
    ulong LeftSiblingAddress,
    ulong RightSiblingAddress,
    T[] Keys,
    ulong[] ChildAddresses
) where T : struct, IBTree1Key
{
    // Child nodes by address. Concurrent because one cached tree serves concurrent lookups.
    private readonly ConcurrentDictionary<ulong, BTree1Node<T>> _cache = new();

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("TREE");

    public static async ValueTask<BTree1Node<T>> Decode(NativeReadContext context, DecodeKeyDelegate<T> decodeKey)
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
            keys[i] = await decodeKey(context).ConfigureAwait(false);
            childAddresses[i] = await superblock.ReadOffset(driver).ConfigureAwait(false);
        }

        keys[entriesUsed] = await decodeKey(context).ConfigureAwait(false);

        return new BTree1Node<T>(
            nodeLevel,
            entriesUsed,
            leftSiblingAddress,
            rightSiblingAddress,
            keys,
            childAddresses
        );
    }

    // NOTE (async propagation): was a property; C# has no async property getters,
    // so this became a method with the same name. No callers exist in the repo today.
    public async ValueTask<BTree1Node<T>> LeftSibling(NativeReadContext context, DecodeKeyDelegate<T> decodeKey)
    {
        context.Driver.SeekRelativeToBaseAddress((long)LeftSiblingAddress);
        return await BTree1Node<T>.Decode(context, decodeKey).ConfigureAwait(false);
    }

    // NOTE (async propagation): was a property; see LeftSibling.
    public async ValueTask<BTree1Node<T>> RightSibling(NativeReadContext context, DecodeKeyDelegate<T> decodeKey)
    {
        context.Driver.SeekRelativeToBaseAddress((long)RightSiblingAddress);
        return await BTree1Node<T>.Decode(context, decodeKey).ConfigureAwait(false);
    }

    // NOTE (async propagation): `out TUserData userData` cannot coexist with `async`
    // (CS1988), so the out parameter became a tuple return. Callers outside this file
    // (H5D_Chunk123_BTree1.cs, NativeGroup.cs) need updating — see report.
    public async ValueTask<(bool Success, TUserData UserData)> TryFindUserData<TUserData>(
        NativeReadContext context,
        DecodeKeyDelegate<T> decodeKey,
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
            if (!_cache.TryGetValue(childAddress, out var subtree))
            {
                context.Driver.SeekRelativeToBaseAddress((long)childAddress);
                subtree = await BTree1Node<T>.Decode(context, decodeKey).ConfigureAwait(false);
                subtree = _cache.GetOrAdd(childAddress, subtree);
            }

            var (success, userData) = await subtree.TryFindUserData(context, decodeKey, compare3, found).ConfigureAwait(false);

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
    public IAsyncEnumerable<BTree1Node<T>> EnumerateNodes(NativeReadContext context, DecodeKeyDelegate<T> decodeKey)
    {
        return EnumerateNodes(context, decodeKey, this);
    }

    private static async IAsyncEnumerable<BTree1Node<T>> EnumerateNodes(
        NativeReadContext context,
        DecodeKeyDelegate<T> decodeKey,
        BTree1Node<T> node)
    {
        // internal node
        if (node.NodeLevel > 0)
        {
            foreach (var address in node.ChildAddresses)
            {
                context.Driver.SeekRelativeToBaseAddress((long)address);

                var childNode = await BTree1Node<T>.Decode(context, decodeKey).ConfigureAwait(false);

                // internal node
                if ((node.NodeLevel - 1) > 0)
                {
                    var internalNodes = EnumerateNodes(context, decodeKey, childNode);

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

    private async ValueTask<(uint index, int cmp)> LocateRecord(Func<T, T, ValueTask<int>> compare3)
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
