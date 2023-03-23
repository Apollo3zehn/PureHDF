using System.Text;

namespace PureHDF.VOL.Native;

internal class FractalHeapHeader
{
    #region Fields

    private byte _version;
    private NativeContext _context;

    #endregion

    #region Constructors

    public FractalHeapHeader(NativeContext context)
    {
        var (driver, superblock) = context;
        _context = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, FractalHeapHeader.Signature);

        // version
        Version = driver.ReadByte();

        // heap ID length
        HeapIdLength = driver.ReadUInt16();

        // I/O filter encoder length
        IOFilterEncodedLength = driver.ReadUInt16();

        // flags
        Flags = (FractalHeapHeaderFlags)driver.ReadByte();

        /* next group */

        // managed objects maximum size
        ManagedObjectsMaximumSize = driver.ReadUInt32();

        // next huge object id
        NextHugeObjectId = superblock.ReadLength(driver);

        // huge objects BTree2 address
        HugeObjectsBTree2Address = superblock.ReadOffset(driver);

        // managed blocks free space amount
        ManagedBlocksFreeSpaceAmount = superblock.ReadLength(driver);

        // managed block free space manager address
        ManagedBlockFreeSpaceManagerAddress = superblock.ReadOffset(driver);

        // heap managed space amount
        HeapManagedSpaceAmount = superblock.ReadLength(driver);

        // heap allocated managed space amount
        HeapAllocatedManagedSpaceAmount = superblock.ReadLength(driver);

        // managed space direct block allocation iterator offset
        ManagedSpaceDirectBlockAllocationIteratorOffset = superblock.ReadLength(driver);

        // heap managed objects count
        HeapManagedObjectsCount = superblock.ReadLength(driver);

        // heap huge objects size
        HeapHugeObjectsSize = superblock.ReadLength(driver);

        // heap huge objects cound
        HeapHugeObjectsCount = superblock.ReadLength(driver);

        // heap tiny objects size
        HeapTinyObjectsSize = superblock.ReadLength(driver);

        // heap tiny objects count
        HeapTinyObjectsCount = superblock.ReadLength(driver);

        /* next group */

        // table width
        TableWidth = driver.ReadUInt16();

        // starting block size
        StartingBlockSize = superblock.ReadLength(driver);

        // maximum direct block size
        MaximumDirectBlockSize = superblock.ReadLength(driver);

        // maximum heap size
        MaximumHeapSize = driver.ReadUInt16();

        // root indirect block rows starting number
        RootIndirectBlockRowsStartingNumber = driver.ReadUInt16();

        // root block address
        RootBlockAddress = superblock.ReadOffset(driver);

        // root indirect block rows count
        RootIndirectBlockRowsCount = driver.ReadUInt16();

        /* next group */

        // filtered root direct block size, I/O filter mask and I/O filter info
        if (IOFilterEncodedLength > 0)
        {
            FilteredRootDirectBlockSize = superblock.ReadLength(driver);
            IOFilterMask = driver.ReadUInt32();
            IOFilterInfo = new FilterPipelineMessage(driver);
        }

        // checksum
        Checksum = driver.ReadUInt32();

        // cache some values
        CalculateBlockSizeTables();
        CalculateHugeObjectsData();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FRHP");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapHeader)} are supported.");

            _version = value;
        }
    }

    public ushort HeapIdLength { get; set; }
    public ushort IOFilterEncodedLength { get; set; }
    public FractalHeapHeaderFlags Flags { get; set; }
    public uint ManagedObjectsMaximumSize { get; set; }

    public ulong NextHugeObjectId { get; set; }
    public ulong HugeObjectsBTree2Address { get; set; }
    public ulong ManagedBlocksFreeSpaceAmount { get; set; }
    public ulong ManagedBlockFreeSpaceManagerAddress { get; set; }
    public ulong HeapManagedSpaceAmount { get; set; }
    public ulong HeapAllocatedManagedSpaceAmount { get; set; }
    public ulong ManagedSpaceDirectBlockAllocationIteratorOffset { get; set; }
    public ulong HeapManagedObjectsCount { get; set; }

    public ulong HeapHugeObjectsSize { get; set; }
    public ulong HeapHugeObjectsCount { get; set; }

    public ulong HeapTinyObjectsSize { get; set; }
    public ulong HeapTinyObjectsCount { get; set; }

    public ushort TableWidth { get; set; }
    public ulong StartingBlockSize { get; set; }
    public ulong MaximumDirectBlockSize { get; set; }
    public ushort MaximumHeapSize { get; set; }
    public ushort RootIndirectBlockRowsStartingNumber { get; set; }
    public ulong RootBlockAddress { get; set; }
    public ushort RootIndirectBlockRowsCount { get; set; }

    public ulong FilteredRootDirectBlockSize { get; set; }
    public uint IOFilterMask { get; set; }
    public FilterPipelineMessage? IOFilterInfo { get; set; }
    public uint Checksum { get; set; }

    // initialized in CalculateBlockSizeTables
    public ulong[] RowBlockSizes { get; private set; } = default!;
    public ulong[] RowBlockOffsets { get; private set; } = default!;

    public uint StartingBits { get; private set; }
    public uint FirstRowBits { get; private set; }
    public uint MaxDirectRows { get; private set; }

    public bool HugeIdsAreDirect { get; private set; }
    public byte HugeIdsSize { get; private set; }

    public bool TinyObjectsAreExtended { get; private set; }

    #endregion

    #region Methods

    // from H5HF__man_op_real
    public ulong GetAddress(ManagedObjectsFractalHeapId heapId)
    {
        FractalHeapDirectBlock directBlock;
        ulong directBlockSize;
        ulong directBlockAddress;

        /* Check for root direct block */
        var isDirectBlock = RootIndirectBlockRowsCount == 0;

        if (isDirectBlock)
        {
            /* Set direct block info */
            directBlockSize = StartingBlockSize;
            directBlockAddress = RootBlockAddress;
        }
        else
        {
            /* Look up indirect block containing direct block */
            var (indirectBlock, entry) = Locate(heapId.Offset);

            /* Set direct block info */
            directBlockSize = RowBlockSizes[entry / TableWidth];
            directBlockAddress = indirectBlock.Entries[entry].Address;
        }

        _context.Driver.Seek((long)directBlockAddress, SeekOrigin.Begin);
        directBlock = new FractalHeapDirectBlock(_context, this);

        /* Compute offset of object within block */
        if (heapId.Offset >= directBlock.BlockOffset + directBlockSize)
            throw new Exception("Object start offset overruns end of direct block.");

        var blockOffset = heapId.Offset - directBlock.BlockOffset;

        /* Check for object's offset in the direct block prefix information */
        if (blockOffset < directBlock.HeaderSize)
            throw new Exception("Object located in prefix of direct block.");

        /* Check for object's length overrunning the end of the direct block */
        if (blockOffset + heapId.Length > directBlockSize)
            throw new Exception("Object overruns end of direct block.");

        return directBlockAddress + blockOffset;
    }

    // from H5HF__man_dblock_locate
    private (FractalHeapIndirectBlock IndirectBlock, ulong entry) Locate(ulong offset)
    {
        var (row, column) = Lookup(offset);

        _context.Driver.Seek((long)RootBlockAddress, SeekOrigin.Begin);
        var indirectBlock = new FractalHeapIndirectBlock(_context, this, RootIndirectBlockRowsCount);

        uint entry;

        while (row >= MaxDirectRows)
        {
            /* Compute # of rows in child indirect block */
            var nrows = (uint)Math.Log(RowBlockSizes[row], 2) - FirstRowBits + 1;

            if (nrows >= indirectBlock.RowCount)
                throw new Exception("Child fractal heap block must be smaller than its parent.");

            /* Compute indirect block's entry */
            entry = row * TableWidth + column;

            /* Locate child indirect block */
            var indirectBlockEntry = indirectBlock.Entries[entry];

            /* Use new indirect block */
            _context.Driver.Seek((long)indirectBlockEntry.Address, SeekOrigin.Begin);
            indirectBlock = new FractalHeapIndirectBlock(_context, this, nrows);

            /* Look up row & column in new indirect block for object */
            (row, column) = Lookup(offset - indirectBlock.BlockOffset);

            if (row >= indirectBlock.RowCount)
                throw new Exception("Child fractal heap block must be smaller than its parent.");
        }

        entry = row * TableWidth + column;

        return (indirectBlock, entry);
    }

    // from H5HF_dtable_lookup
    private (uint Row, uint Column) Lookup(ulong offset)
    {
        uint row;
        uint column;

        if (offset < StartingBlockSize * TableWidth)
        {
            row = 0;
            column = (uint)(offset / StartingBlockSize);
        }
        else
        {
            var highBit = (uint)Math.Log(offset, 2);
            ulong offMask = (ulong)(1 << (int)highBit);
            row = highBit - FirstRowBits + 1;
            column = (uint)((offset - offMask) / RowBlockSizes[row]);
        }

        return (row, column);
    }

    private void CalculateBlockSizeTables()
    {
        // from H5HFdtable.c
        StartingBits = (uint)Math.Log(StartingBlockSize, 2);
        FirstRowBits = (uint)(StartingBits + Math.Log(TableWidth, 2));

        var maxDirectBits = (uint)Math.Log(MaximumDirectBlockSize, 2);
        MaxDirectRows = maxDirectBits - StartingBits + 2;

        var maxRootRows = MaximumHeapSize - FirstRowBits;

        RowBlockSizes = new ulong[maxRootRows];
        RowBlockOffsets = new ulong[maxRootRows];

        var tmpBlockSize = StartingBlockSize;
        var accumulatedBlockOffset = StartingBlockSize * TableWidth;

        RowBlockSizes[0] = tmpBlockSize;
        RowBlockOffsets[0] = 0;

        for (ulong i = 1; i < maxRootRows; i++)
        {
            RowBlockSizes[i] = tmpBlockSize;
            RowBlockOffsets[i] = accumulatedBlockOffset;
            tmpBlockSize *= 2;
            accumulatedBlockOffset *= 2;
        }
    }

    private void CalculateHugeObjectsData()
    {
        // H5HFhuge.c (H5HF_huge_init)

        var superblock = _context.Superblock;

        // with filter
        if (IOFilterEncodedLength > 0)
        {
            // length of fractal heap id for huge objects (sub-type 4)
            var actualLength = superblock.OffsetsSize + superblock.LengthsSize + 4 + superblock.LengthsSize;

            if ((HeapIdLength - 1) >= actualLength)
            {
                /* Indicate that v2 B-tree doesn't have to be used to locate object */
                HugeIdsAreDirect = true;

                /* Set the size of 'huge' object IDs */
                // TODO: Correct? Why is here not "+4"?
                HugeIdsSize = (byte)(superblock.OffsetsSize + superblock.LengthsSize + superblock.LengthsSize);
            }
            else
            {
                /* Indicate that v2 B-tree must be used to access object */
                HugeIdsAreDirect = false;
            }
        }
        // without filter
        else
        {
            // length of fractal heap id for huge objects (sub-type 3)
            var actualLength = superblock.OffsetsSize + superblock.LengthsSize;

            if ((HeapIdLength - 1) >= actualLength)
            {
                /* Indicate that v2 B-tree doesn't have to be used to locate object */
                HugeIdsAreDirect = true;

                /* Set the size of 'huge' object IDs */
                HugeIdsSize = (byte)actualLength;
            }
            else
            {
                /* Indicate that v2 B-tree must be used to access object */
                HugeIdsAreDirect = false;
            }
        }

        // set huge id size for indirect access
        if (!HugeIdsAreDirect)
        {
            /* Set the size of 'huge' object ID */
            if ((HeapIdLength - 1) < sizeof(ulong))
                HugeIdsSize = (byte)(HeapIdLength - 1);
            else
                HugeIdsSize = sizeof(ulong);
        }
    }

    private void CalculateTinyObjectsData()
    {
        // H5HFtiny.c (H5HF_tiny_init)

        /* Compute information about 'tiny' objects for the heap */

        /* Check if tiny objects need an extra byte for their length
         * (account for boundary condition when length of an object would need an
         *  extra byte, but using that byte means that the extra length byte is
         *  unnecessary)
         */
        if ((HeapIdLength - 1) <= 16)
        {
            TinyObjectsAreExtended = false;
        }
        else if ((HeapIdLength - 1) <= (16 + 1))
        {
            TinyObjectsAreExtended = false;
        }
        else
        {
            TinyObjectsAreExtended = true;
        }
    }

    #endregion
}