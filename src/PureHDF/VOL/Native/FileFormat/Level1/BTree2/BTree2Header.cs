using System.Collections.Concurrent;
using System.Text;

namespace PureHDF.VOL.Native;

// TODO should this be a class? Benchmark required
internal record class BTree2Header<T>(
    NativeReadContext Context,
    Func<ValueTask<T>> DecodeKey,
    BTree2Type Type,
    ushort Depth,
    BTree2NodePointer RootNodePointer,
    BTree2NodeInfo[] NodeInfos,
    byte MaxRecordCountSize
) where T : struct, IBTree2Record
{
    private byte _version;

    private ConcurrentDictionary<ulong, BTree2InternalNode<T>> _addressToNodeMap { get; } = new();

    public static async ValueTask<BTree2Header<T>> Decode(
        NativeReadContext context,
        Func<ValueTask<T>> decodeKey
    )
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // type
        var type = (BTree2Type)(await driver.ReadByte().ConfigureAwait(false));

        // node size
        var nodeSize = await driver.ReadUInt32().ConfigureAwait(false);

        // record size
        var recordSize = await driver.ReadUInt16().ConfigureAwait(false);

        // depth
        var depth = await driver.ReadUInt16().ConfigureAwait(false);

        // split percent
        var splitPercent = await driver.ReadByte().ConfigureAwait(false);

        // merge percent
        var mergePercent = await driver.ReadByte().ConfigureAwait(false);

        // root node address
        var rootNodePointerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var rootNodePointerRecordCount = await driver.ReadUInt16().ConfigureAwait(false);
        var rootNodePointerTotalRecordCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        var rootNodePointer = new BTree2NodePointer(
            Address: rootNodePointerAddress,
            RecordCount: rootNodePointerRecordCount,
            TotalRecordCount: rootNodePointerTotalRecordCount
        );

        // checksum
        var checksum = await driver.ReadUInt32().ConfigureAwait(false);

        // from H5B2hdr.c
        var nodeInfos = new BTree2NodeInfo[depth + 1];

        /* Initialize leaf node info */
        var fixedSizeOverhead = 4U + 1U + 1U + 4U; // signature, version, type, checksum
        var maxLeafRecordCount = (nodeSize - fixedSizeOverhead) / recordSize;

        nodeInfos[0] = new BTree2NodeInfo(
            MaxRecordCount: maxLeafRecordCount,
            SplitRecordCount: nodeInfos[0].MaxRecordCount * splitPercent / 100,
            MergeRecordCount: nodeInfos[0].MaxRecordCount * mergePercent / 100,
            CumulatedTotalRecordCount: nodeInfos[0].MaxRecordCount,
            CumulatedTotalRecordCountSize: 0
        );

        /* Compute size to store # of records in each node */
        /* (uses leaf # of records because its the largest) */
        var maxRecordCountSize = (byte)MathUtils.FindMinByteCount(nodeInfos[0].MaxRecordCount);

        /* Initialize internal node info */
        if (depth > 0)
        {
            for (int i = 1; i < depth + 1; i++)
            {
                var pointerSize = (uint)(superblock.OffsetsSize + maxRecordCountSize + nodeInfos[i - 1].CumulatedTotalRecordCountSize);
                var maxInternalRecordCount = (nodeSize - (fixedSizeOverhead + pointerSize)) / recordSize + pointerSize;

                var cumulatedTotalRecordCount =
                    (maxInternalRecordCount + 1) *
                    nodeInfos[i - 1].MaxRecordCount + maxInternalRecordCount;

                nodeInfos[i] = new BTree2NodeInfo(
                    MaxRecordCount: maxInternalRecordCount,
                    SplitRecordCount: maxInternalRecordCount * splitPercent / 100,
                    MergeRecordCount: maxInternalRecordCount * mergePercent / 100,
                    CumulatedTotalRecordCount: cumulatedTotalRecordCount,
                    CumulatedTotalRecordCountSize: (byte)MathUtils.FindMinByteCount(cumulatedTotalRecordCount)
                );
            }
        }

        return new BTree2Header<T>(
            context,
            decodeKey,
            type,
            depth,
            rootNodePointer,
            nodeInfos,
            maxRecordCountSize
        )
        {
            Version = version
        };
    }

    public static byte[] Signature = Encoding.ASCII.GetBytes("BTHD");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(BTree2Header<T>)} are supported.");

            _version = value;
        }
    }

    // NOTE (async propagation): was a property; C# has no async property getters,
    // so this became a method with the same name. Callers outside this file need
    // updating — see report.
    public async ValueTask<BTree2Node<T>?> RootNode()
    {
        if (Context.Superblock.IsUndefinedAddress(RootNodePointer.Address))
        {
            return null;
        }
        else
        {
            Context.Driver.SeekRelativeToBaseAddress((long)RootNodePointer.Address);

            return Depth != 0

                ? await BTree2InternalNode<T>.Decode(
                    Context,
                    this,
                    RootNodePointer.RecordCount,
                    Depth,
                    DecodeKey).ConfigureAwait(false)

                : await BTree2LeafNode<T>.Decode(
                    Context.Driver,
                    this,
                    RootNodePointer.RecordCount,
                    DecodeKey).ConfigureAwait(false);
        }
    }

    // NOTE (async propagation): `out T result` cannot coexist with `async` (CS1988),
    // so the out parameter became a tuple return. Callers outside this file need
    // updating — see report.
    public async ValueTask<(bool Success, T Result)> TryFindRecord(Func<T, ValueTask<int>> compare)
    {
        /* H5B2.c (H5B2_find) */
        int cmp;
        uint index = 0;
        BTree2NodePosition curr_pos;

        /* Make copy of the root node pointer to start search with */
        var currentNodePointer = RootNodePointer;

        /* Check for empty tree */
        if (currentNodePointer.RecordCount == 0)
            return (false, default);

        // TODO: Optimizations missing.

        /* Current depth of the tree */
        var depth = Depth;

        /* Walk down B-tree to find record or leaf node where record is located */
        cmp = -1;
        curr_pos = BTree2NodePosition.Root;

        while (depth > 0)
        {
            var address = currentNodePointer.Address;

            if (!_addressToNodeMap.TryGetValue(address, out var internalNode))
            {
                Context.Driver.SeekRelativeToBaseAddress((long)currentNodePointer.Address);

                internalNode = await BTree2InternalNode<T>.Decode(
                    Context,
                    this,
                    currentNodePointer.RecordCount,
                    depth,
                    DecodeKey
                ).ConfigureAwait(false) ?? throw new Exception("Unable to load B-tree internal node.");

                internalNode = _addressToNodeMap.GetOrAdd(address, internalNode);
            }

            /* Locate node pointer for child */
            (index, cmp) = await LocateRecord(internalNode.Records, compare).ConfigureAwait(false);

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
                return (true, internalNode.Records[index]);
            }

            /* Decrement depth we're at in B-tree */
            depth--;
        }

        {
            Context.Driver.SeekRelativeToBaseAddress((long)currentNodePointer.Address);

            var leafNode = await BTree2LeafNode<T>.Decode(
                Context.Driver,
                this,
                currentNodePointer.RecordCount,
                DecodeKey).ConfigureAwait(false);

            /* Locate record */
            (index, cmp) = await BTree2Header<T>.LocateRecord(leafNode.Records, compare).ConfigureAwait(false);

            if (cmp == 0)
            {
                return (true, leafNode.Records[index]);

                // TODO: Optimizations missing.
            }
        }

        return (false, default);
    }

    // NOTE (async propagation): iterator that reads becomes IAsyncEnumerable<T> (rule 8).
    // Callers outside this file need updating — see report.
    public async IAsyncEnumerable<T> EnumerateRecords()
    {
        var rootNode = await RootNode().ConfigureAwait(false);

        if (rootNode is not null)
        {
            await foreach (var record in EnumerateRecords(rootNode, Depth))
            {
                yield return record;
            }
        }
    }

    private async IAsyncEnumerable<T> EnumerateRecords(BTree2Node<T> node, ushort nodeLevel)
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
                Context.Driver.SeekRelativeToBaseAddress((long)nodePointer.Address);
                var childNodeLevel = (ushort)(nodeLevel - 1);

                // internal node
                if (childNodeLevel > 0)
                {
                    var childNode = await BTree2InternalNode<T>.Decode(
                        Context,
                        this,
                        nodePointer.RecordCount,
                        childNodeLevel,
                        DecodeKey).ConfigureAwait(false);

                    await foreach (var record in EnumerateRecords(childNode, childNodeLevel))
                    {
                        yield return record;
                    }
                }
                // leaf node
                else
                {
                    var childNode = await BTree2LeafNode<T>.Decode(
                        Context.Driver,
                        this,
                        nodePointer.RecordCount,
                        DecodeKey).ConfigureAwait(false);

                    foreach (var record in childNode.Records)
                    {
                        yield return record;
                    }
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

    private static async ValueTask<(uint index, int cmp)> LocateRecord(
        T[] records,
        Func<T, ValueTask<int>> compare)
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
            cmp = await compare(records[index]).ConfigureAwait(false);

            if (cmp < 0)
                high = index;
            else
                low = index + 1;
        }

        return (index, cmp);
    }
}