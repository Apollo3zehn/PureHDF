using System.Text;

namespace PureHDF.VOL.Native;

// TODO should this be a class? Benchmark required
internal record class BTree2Header<T>(
    NativeContext Context,
    Func<T> DecodeKey,
    BTree2Type Type,
    ushort Depth,
    BTree2NodePointer RootNodePointer,
    BTree2NodeInfo[] NodeInfos,
    byte MaxRecordCountSize
) where T : struct, IBTree2Record
{
    private byte _version;

    public static BTree2Header<T> Decode(
        NativeContext context,
        Func<T> decodeKey
    )
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, BTree2Header<T>.Signature);

        // version
        var version = driver.ReadByte();

        // type
        var type = (BTree2Type)driver.ReadByte();

        // node size
        var nodeSize = driver.ReadUInt32();

        // record size
        var recordSize = driver.ReadUInt16();

        // depth
        var depth = driver.ReadUInt16();

        // split percent
        var splitPercent = driver.ReadByte();

        // merge percent
        var mergePercent = driver.ReadByte();

        // root node address
        var rootNodePointer = new BTree2NodePointer(
            Address: superblock.ReadOffset(driver),
            RecordCount: driver.ReadUInt16(),
            TotalRecordCount: superblock.ReadLength(driver)
        );

        // checksum
        var checksum = driver.ReadUInt32();

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

    public BTree2Node<T>? RootNode
    {
        get
        {
            if (Context.Superblock.IsUndefinedAddress(RootNodePointer.Address))
            {
                return null;
            }
            else
            {
                Context.Driver.Seek((long)RootNodePointer.Address, SeekOrigin.Begin);

                return Depth != 0

                    ? BTree2InternalNode<T>.Decode(
                        Context, 
                        this,
                        RootNodePointer.RecordCount, 
                        Depth, 
                        DecodeKey)

                    : BTree2LeafNode<T>.Decode(
                        Context.Driver, 
                        this, 
                        RootNodePointer.RecordCount, 
                        DecodeKey);
            }
        }
    }

    public bool TryFindRecord(out T result, Func<T, int> compare)
    {
        /* H5B2.c (H5B2_find) */
        int cmp;
        uint index = 0;
        BTree2NodePosition curr_pos;
        result = default;

        /* Make copy of the root node pointer to start search with */
        var currentNodePointer = RootNodePointer;

        /* Check for empty tree */
        if (currentNodePointer.RecordCount == 0)
            return false;

        // TODO: Optimizations missing.

        /* Current depth of the tree */
        var depth = Depth;

        /* Walk down B-tree to find record or leaf node where record is located */
        cmp = -1;
        curr_pos = BTree2NodePosition.Root;

        while (depth > 0)
        {
            Context.Driver.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);
            
            var internalNode = BTree2InternalNode<T>.Decode(
                Context, 
                this, 
                currentNodePointer.RecordCount, 
                depth, 
                DecodeKey) 
                ?? throw new Exception("Unable to load B-tree internal node.");

            /* Locate node pointer for child */
            (index, cmp) = BTree2Header<T>.LocateRecord(internalNode.Records, compare);

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
                result = internalNode.Records[index];
                return true;
            }

            /* Decrement depth we're at in B-tree */
            depth--;
        }

        {
            Context.Driver.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);

            var leafNode = BTree2LeafNode<T>.Decode(
                Context.Driver,
                this,
                currentNodePointer.RecordCount,
                DecodeKey);

            /* Locate record */
            (index, cmp) = BTree2Header<T>.LocateRecord(leafNode.Records, compare);

            if (cmp == 0)
            {
                result = leafNode.Records[index];
                return true;

                // TODO: Optimizations missing.
            }
        }

        return false;
    }

    public IEnumerable<T> EnumerateRecords()
    {
        var rootNode = RootNode;

        if (rootNode is not null)
            return EnumerateRecords(rootNode, Depth);
            
        else
            return new List<T>();
    }

    private IEnumerable<T> EnumerateRecords(BTree2Node<T> node, ushort nodeLevel)
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
                Context.Driver.Seek((long)nodePointer.Address, SeekOrigin.Begin);
                var childNodeLevel = (ushort)(nodeLevel - 1);
                IEnumerable<T> childRecords;

                // internal node
                if (childNodeLevel > 0)
                {
                    var childNode = BTree2InternalNode<T>.Decode(
                        Context, 
                        this, 
                        nodePointer.RecordCount, 
                        childNodeLevel, 
                        DecodeKey);

                    childRecords = EnumerateRecords(childNode, childNodeLevel);
                }
                // leaf node
                else
                {
                    var childNode = BTree2LeafNode<T>.Decode(
                        Context.Driver, 
                        this, 
                        nodePointer.RecordCount,
                        DecodeKey);

                    childRecords = childNode.Records;
                }

                foreach (var record in childRecords)
                {
                    yield return record;
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

    private static (uint index, int cmp) LocateRecord(
        T[] records,
        Func<T, int> compare)
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
            cmp = compare(records[index]);

            if (cmp < 0)
                high = index;
            else
                low = index + 1;
        }

        return (index, cmp);
    }
}