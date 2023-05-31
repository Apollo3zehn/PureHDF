using System.Text;

namespace PureHDF.VOL.Native;

internal class BTree2Header<T> where T : struct, IBTree2Record
{
    #region Fields

    private readonly Func<T> _decodeKey;

    private NativeContext _context;
    private byte _version;

    #endregion

    #region Constructors

    public BTree2Header(NativeContext context, Func<T> decodeKey)
    {
        var (driver, superblock) = context;
        _context = context;

        _decodeKey = decodeKey;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, BTree2Header<T>.Signature);

        // version
        Version = driver.ReadByte();

        // type
        Type = (BTree2Type)driver.ReadByte();

        // node size
        NodeSize = driver.ReadUInt32();

        // record size
        RecordSize = driver.ReadUInt16();

        // depth
        Depth = driver.ReadUInt16();

        // split percent
        SplitPercent = driver.ReadByte();

        // merge percent
        MergePercent = driver.ReadByte();

        // root node address
        RootNodePointer = new BTree2NodePointer()
        {
            Address = superblock.ReadOffset(driver),
            RecordCount = driver.ReadUInt16(),
            TotalRecordCount = superblock.ReadLength(driver)
        };

        // checksum
        Checksum = driver.ReadUInt32();

        // from H5B2hdr.c
        NodeInfos = new BTree2NodeInfo[Depth + 1];

        /* Initialize leaf node info */
        var fixedSizeOverhead = 4U + 1U + 1U + 4U; // signature, version, type, checksum
        var maxLeafRecordCount = (NodeSize - fixedSizeOverhead) / RecordSize;
        NodeInfos[0].MaxRecordCount = maxLeafRecordCount;
        NodeInfos[0].SplitRecordCount = (NodeInfos[0].MaxRecordCount * SplitPercent) / 100;
        NodeInfos[0].MergeRecordCount = (NodeInfos[0].MaxRecordCount * MergePercent) / 100;
        NodeInfos[0].CumulatedTotalRecordCount = NodeInfos[0].MaxRecordCount;
        NodeInfos[0].CumulatedTotalRecordCountSize = 0;

        /* Compute size to store # of records in each node */
        /* (uses leaf # of records because its the largest) */
        MaxRecordCountSize = (byte)Utils.FindMinByteCount(NodeInfos[0].MaxRecordCount); ;

        /* Initialize internal node info */
        if (Depth > 0)
        {
            for (int i = 1; i < Depth + 1; i++)
            {
                var pointerSize = (uint)(superblock.OffsetsSize + MaxRecordCountSize + NodeInfos[i - 1].CumulatedTotalRecordCountSize);
                var maxInternalRecordCount = (NodeSize - (fixedSizeOverhead + pointerSize)) / RecordSize + pointerSize;

                NodeInfos[i].MaxRecordCount = maxInternalRecordCount;
                NodeInfos[i].SplitRecordCount = (NodeInfos[i].MaxRecordCount * SplitPercent) / 100;
                NodeInfos[i].MergeRecordCount = (NodeInfos[i].MaxRecordCount * MergePercent) / 100;
                NodeInfos[i].CumulatedTotalRecordCount =
                    (NodeInfos[i].MaxRecordCount + 1) *
                     NodeInfos[i - 1].MaxRecordCount + NodeInfos[i].MaxRecordCount;
                NodeInfos[i].CumulatedTotalRecordCountSize = (byte)Utils.FindMinByteCount(NodeInfos[i].CumulatedTotalRecordCount);
            }
        }
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTHD");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(BTree2Header<T>)} are supported.");

            _version = value;
        }
    }

    public BTree2Type Type { get; set; }
    public uint NodeSize { get; set; }
    public ushort RecordSize { get; set; }
    public ushort Depth { get; set; }
    public byte SplitPercent { get; set; }
    public byte MergePercent { get; set; }
    public BTree2NodePointer RootNodePointer { get; set; }
    public uint Checksum { get; set; }

    public BTree2Node<T>? RootNode
    {
        get
        {
            if (_context.Superblock.IsUndefinedAddress(RootNodePointer.Address))
            {
                return null;
            }
            else
            {
                _context.Driver.Seek((long)RootNodePointer.Address, SeekOrigin.Begin);

                return Depth != 0
                    ? (BTree2Node<T>)new BTree2InternalNode<T>(_context, this, RootNodePointer.RecordCount, Depth, _decodeKey)
                    : (BTree2Node<T>)new BTree2LeafNode<T>(_context.Driver, this, RootNodePointer.RecordCount, _decodeKey);
            }
        }
    }

    internal BTree2NodeInfo[] NodeInfos { get; }

    internal byte MaxRecordCountSize { get; }

    internal T MinNativeRec { get; set; }

    internal T MaxNativeRec { get; set; }

    #endregion

    #region Methods

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
            _context.Driver.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);
            
            var internalNode = new BTree2InternalNode<T>(_context, this, currentNodePointer.RecordCount, depth, _decodeKey) 
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
            _context.Driver.Seek((long)currentNodePointer.Address, SeekOrigin.Begin);
            var leafNode = new BTree2LeafNode<T>(_context.Driver, this, currentNodePointer.RecordCount, _decodeKey);

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
                _context.Driver.Seek((long)nodePointer.Address, SeekOrigin.Begin);
                var childNodeLevel = (ushort)(nodeLevel - 1);

                IEnumerable<T> childRecords;

                // internal node
                if (childNodeLevel > 0)
                {
                    var childNode = new BTree2InternalNode<T>(_context, this, nodePointer.RecordCount, childNodeLevel, _decodeKey);
                    childRecords = EnumerateRecords(childNode, childNodeLevel);
                }
                // leaf node
                else
                {
                    var childNode = new BTree2LeafNode<T>(_context.Driver, this, nodePointer.RecordCount, _decodeKey);
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

    #endregion
}