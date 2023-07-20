using System.Text;

namespace PureHDF.VOL.Native;

// this should be a class because it has so many fields
internal record class FractalHeapHeader(
    NativeContext Context,
    
    ushort HeapIdLength,
    ushort IOFilterEncodedLength,
    FractalHeapHeaderFlags Flags,
    uint ManagedObjectsMaximumSize,

    ulong NextHugeObjectId,
    ulong HugeObjectsBTree2Address,
    ulong ManagedBlocksFreeSpaceAmount,
    ulong ManagedBlockFreeSpaceManagerAddress,
    ulong HeapManagedSpaceAmount,
    ulong HeapAllocatedManagedSpaceAmount,
    ulong ManagedSpaceDirectBlockAllocationIteratorOffset,
    ulong HeapManagedObjectsCount,

    ulong HeapHugeObjectsSize,
    ulong HeapHugeObjectsCount,

    ulong HeapTinyObjectsSize,
    ulong HeapTinyObjectsCount,

    ushort TableWidth,
    ulong StartingBlockSize,
    ulong MaximumDirectBlockSize,
    ushort MaximumHeapSize,
    ushort RootIndirectBlockRowsStartingNumber,
    ulong RootBlockAddress,
    ushort RootIndirectBlockRowsCount,

    ulong FilteredRootDirectBlockSize,
    uint IOFilterMask,
    FilterPipelineMessage? IOFilterInfo,
    uint Checksum,

    ulong[] RowBlockSizes,
    ulong[] RowBlockOffsets,

    uint StartingBits,
    uint FirstRowBits,
    uint MaxDirectRows,

    bool HugeIdsAreDirect,
    byte HugeIdsSize,

    bool TinyObjectsAreExtended
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FRHP");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapHeader)} are supported.");

            _version = value;
        }
    }

    public static FractalHeapHeader Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // heap ID length
        var heapIdLength = driver.ReadUInt16();

        // I/O filter encoder length
        var ioFilterEncodedLength = driver.ReadUInt16();

        // flags
        var flags = (FractalHeapHeaderFlags)driver.ReadByte();

        /* next group */

        // managed objects maximum size
        var managedObjectsMaximumSize = driver.ReadUInt32();

        // next huge object id
        var nextHugeObjectId = superblock.ReadLength(driver);

        // huge objects BTree2 address
        var hugeObjectsBTree2Address = superblock.ReadOffset(driver);

        // managed blocks free space amount
        var managedBlocksFreeSpaceAmount = superblock.ReadLength(driver);

        // managed block free space manager address
        var managedBlockFreeSpaceManagerAddress = superblock.ReadOffset(driver);

        // heap managed space amount
        var heapManagedSpaceAmount = superblock.ReadLength(driver);

        // heap allocated managed space amount
        var heapAllocatedManagedSpaceAmount = superblock.ReadLength(driver);

        // managed space direct block allocation iterator offset
        var managedSpaceDirectBlockAllocationIteratorOffset = superblock.ReadLength(driver);

        // heap managed objects count
        var heapManagedObjectsCount = superblock.ReadLength(driver);

        // heap huge objects size
        var heapHugeObjectsSize = superblock.ReadLength(driver);

        // heap huge objects cound
        var heapHugeObjectsCount = superblock.ReadLength(driver);

        // heap tiny objects size
        var heapTinyObjectsSize = superblock.ReadLength(driver);

        // heap tiny objects count
        var heapTinyObjectsCount = superblock.ReadLength(driver);

        /* next group */

        // table width
        var tableWidth = driver.ReadUInt16();

        // starting block size
        var startingBlockSize = superblock.ReadLength(driver);

        // maximum direct block size
        var maximumDirectBlockSize = superblock.ReadLength(driver);

        // maximum heap size
        var maximumHeapSize = driver.ReadUInt16();

        // root indirect block rows starting number
        var rootIndirectBlockRowsStartingNumber = driver.ReadUInt16();

        // root block address
        var rootBlockAddress = superblock.ReadOffset(driver);

        // root indirect block rows count
        var rootIndirectBlockRowsCount = driver.ReadUInt16();

        /* next group */

        // filtered root direct block size, I/O filter mask and I/O filter inf
        var filteredRootDirectBlockSize = default(ulong);
        var ioFilterMask = default(uint);
        var ioFilterInfo = default(FilterPipelineMessage?);

        if (ioFilterEncodedLength > 0)
        {
            filteredRootDirectBlockSize = superblock.ReadLength(driver);
            ioFilterMask = driver.ReadUInt32();
            ioFilterInfo = FilterPipelineMessage.Decode(driver);
        }

        // checksum
        var checksum = driver.ReadUInt32();

        // cache some values
        ulong[] rowBlockSizes;
        ulong[] rowBlockOffsets;

        uint startingBits;
        uint firstRowBits;
        uint maxDirectRows;

        CalculateBlockSizeTables();

        void CalculateBlockSizeTables()
        {
            // from H5HFdtable.c
            startingBits = (uint)Math.Log(startingBlockSize, 2);
            firstRowBits = (uint)(startingBits + Math.Log(tableWidth, 2));

            var maxDirectBits = (uint)Math.Log(maximumDirectBlockSize, 2);
            maxDirectRows = maxDirectBits - startingBits + 2;

            var maxRootRows = maximumHeapSize - firstRowBits;

            rowBlockSizes = new ulong[maxRootRows];
            rowBlockOffsets = new ulong[maxRootRows];

            var tmpBlockSize = startingBlockSize;
            var accumulatedBlockOffset = startingBlockSize * tableWidth;

            rowBlockSizes[0] = tmpBlockSize;
            rowBlockOffsets[0] = 0;

            for (ulong i = 1; i < maxRootRows; i++)
            {
                rowBlockSizes[i] = tmpBlockSize;
                rowBlockOffsets[i] = accumulatedBlockOffset;
                tmpBlockSize *= 2;
                accumulatedBlockOffset *= 2;
            }
        }

        bool hugeIdsAreDirect;
        var hugeIdsSize = default(byte);

        var tinyObjectsAreExtended = default(bool);

        CalculateHugeObjectsData();

        void CalculateHugeObjectsData()
        {
            // H5HFhuge.c (H5HF_huge_init)

            // with filter
            if (ioFilterEncodedLength > 0)
            {
                // length of fractal heap id for huge objects (sub-type 4)
                var actualLength = superblock.OffsetsSize + superblock.LengthsSize + 4 + superblock.LengthsSize;

                if ((heapIdLength - 1) >= actualLength)
                {
                    /* Indicate that v2 B-tree doesn't have to be used to locate object */
                    hugeIdsAreDirect = true;

                    /* Set the size of 'huge' object IDs */
                    // TODO: Correct? Why is here not "+4"?
                    hugeIdsSize = (byte)(superblock.OffsetsSize + superblock.LengthsSize + superblock.LengthsSize);
                }
                else
                {
                    /* Indicate that v2 B-tree must be used to access object */
                    hugeIdsAreDirect = false;
                }
            }
            // without filter
            else
            {
                // length of fractal heap id for huge objects (sub-type 3)
                var actualLength = superblock.OffsetsSize + superblock.LengthsSize;

                if ((heapIdLength - 1) >= actualLength)
                {
                    /* Indicate that v2 B-tree doesn't have to be used to locate object */
                    hugeIdsAreDirect = true;

                    /* Set the size of 'huge' object IDs */
                    hugeIdsSize = (byte)actualLength;
                }
                else
                {
                    /* Indicate that v2 B-tree must be used to access object */
                    hugeIdsAreDirect = false;
                }
            }

            // set huge id size for indirect access
            if (!hugeIdsAreDirect)
            {
                /* Set the size of 'huge' object ID */
                if ((heapIdLength - 1) < sizeof(ulong))
                    hugeIdsSize = (byte)(heapIdLength - 1);
                else
                    hugeIdsSize = sizeof(ulong);
            }

            // void CalculateTinyObjectsData()
            // {
            //     // H5HFtiny.c (H5HF_tiny_init)

            //     /* Compute information about 'tiny' objects for the heap */

            //     /* Check if tiny objects need an extra byte for their length
            //      * (account for boundary condition when length of an object would need an
            //      *  extra byte, but using that byte means that the extra length byte is
            //      *  unnecessary)
            //      */
            //     if ((HeapIdLength - 1) <= 16)
            //     {
            //         TinyObjectsAreExtended = false;
            //     }
            //     else if ((HeapIdLength - 1) <= (16 + 1))
            //     {
            //         TinyObjectsAreExtended = false;
            //     }
            //     else
            //     {
            //         TinyObjectsAreExtended = true;
            //     }
            // }
        }

        return new FractalHeapHeader(
            Context: context,

            HeapIdLength: heapIdLength,
            IOFilterEncodedLength: ioFilterEncodedLength,
            Flags: flags,
            ManagedObjectsMaximumSize: managedObjectsMaximumSize,

            NextHugeObjectId: nextHugeObjectId,
            HugeObjectsBTree2Address: hugeObjectsBTree2Address,
            ManagedBlocksFreeSpaceAmount: managedBlocksFreeSpaceAmount,
            ManagedBlockFreeSpaceManagerAddress: managedBlockFreeSpaceManagerAddress,
            HeapManagedSpaceAmount: heapManagedSpaceAmount,
            HeapAllocatedManagedSpaceAmount: heapAllocatedManagedSpaceAmount,
            ManagedSpaceDirectBlockAllocationIteratorOffset: managedSpaceDirectBlockAllocationIteratorOffset,
            HeapManagedObjectsCount: heapManagedObjectsCount,

            HeapHugeObjectsSize: heapHugeObjectsSize,
            HeapHugeObjectsCount: heapHugeObjectsCount,

            HeapTinyObjectsSize: heapTinyObjectsSize,
            HeapTinyObjectsCount: heapTinyObjectsCount,

            TableWidth: tableWidth,
            StartingBlockSize: startingBlockSize,
            MaximumDirectBlockSize: maximumDirectBlockSize,
            MaximumHeapSize: maximumHeapSize,
            RootIndirectBlockRowsStartingNumber: rootIndirectBlockRowsStartingNumber,
            RootBlockAddress: rootBlockAddress,
            RootIndirectBlockRowsCount: rootIndirectBlockRowsCount,

            FilteredRootDirectBlockSize: filteredRootDirectBlockSize,
            IOFilterMask: ioFilterMask,
            IOFilterInfo: ioFilterInfo,
            Checksum: checksum,

            RowBlockSizes: rowBlockSizes,
            RowBlockOffsets: rowBlockOffsets,

            startingBits,
            firstRowBits,
            maxDirectRows,

            hugeIdsAreDirect,
            hugeIdsSize,

            tinyObjectsAreExtended
        )
        {
            Version = version
        };
    }

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

        Context.Driver.Seek((long)directBlockAddress, SeekOrigin.Begin);
        directBlock = FractalHeapDirectBlock.Decode(Context, this);

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

        Context.Driver.Seek((long)RootBlockAddress, SeekOrigin.Begin);
        var indirectBlock = FractalHeapIndirectBlock.Decode(Context, this, RootIndirectBlockRowsCount);

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
            Context.Driver.Seek((long)indirectBlockEntry.Address, SeekOrigin.Begin);
            indirectBlock = FractalHeapIndirectBlock.Decode(Context, this, nrows);

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
}