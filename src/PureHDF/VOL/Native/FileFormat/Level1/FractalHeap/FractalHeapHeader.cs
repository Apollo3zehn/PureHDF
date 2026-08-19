using System.Text;

namespace PureHDF.VOL.Native;

// CONCURRENCY / CACHING: holds no NativeReadContext, so that a decoded heap header can be cached per
// file (see NativeCache.GetStructure) and shared by concurrent navigation operations. The context is
// passed per call to the two methods that read - everything else here is decoded format data.
//
// this should be a class because it has so many fields
internal sealed record class FractalHeapHeader(
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

    public static async ValueTask<FractalHeapHeader> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // heap ID length
        var heapIdLength = await driver.ReadUInt16().ConfigureAwait(false);

        // I/O filter encoder length
        var ioFilterEncodedLength = await driver.ReadUInt16().ConfigureAwait(false);

        // flags
        var flags = (FractalHeapHeaderFlags)(await driver.ReadByte().ConfigureAwait(false));

        /* next group */

        // managed objects maximum size
        var managedObjectsMaximumSize = await driver.ReadUInt32().ConfigureAwait(false);

        // next huge object id
        var nextHugeObjectId = await superblock.ReadLength(driver).ConfigureAwait(false);

        // huge objects BTree2 address
        var hugeObjectsBTree2Address = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // managed blocks free space amount
        var managedBlocksFreeSpaceAmount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // managed block free space manager address
        var managedBlockFreeSpaceManagerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // heap managed space amount
        var heapManagedSpaceAmount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap allocated managed space amount
        var heapAllocatedManagedSpaceAmount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // managed space direct block allocation iterator offset
        var managedSpaceDirectBlockAllocationIteratorOffset = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap managed objects count
        var heapManagedObjectsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap huge objects size
        var heapHugeObjectsSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap huge objects cound
        var heapHugeObjectsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap tiny objects size
        var heapTinyObjectsSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // heap tiny objects count
        var heapTinyObjectsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        /* next group */

        // table width
        var tableWidth = await driver.ReadUInt16().ConfigureAwait(false);

        // starting block size
        var startingBlockSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // maximum direct block size
        var maximumDirectBlockSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // maximum heap size
        var maximumHeapSize = await driver.ReadUInt16().ConfigureAwait(false);

        // root indirect block rows starting number
        var rootIndirectBlockRowsStartingNumber = await driver.ReadUInt16().ConfigureAwait(false);

        // root block address
        var rootBlockAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // root indirect block rows count
        var rootIndirectBlockRowsCount = await driver.ReadUInt16().ConfigureAwait(false);

        /* next group */

        // filtered root direct block size, I/O filter mask and I/O filter inf
        var filteredRootDirectBlockSize = default(ulong);
        var ioFilterMask = default(uint);
        var ioFilterInfo = default(FilterPipelineMessage?);

        if (ioFilterEncodedLength > 0)
        {
            filteredRootDirectBlockSize = await superblock.ReadLength(driver).ConfigureAwait(false);
            ioFilterMask = await driver.ReadUInt32().ConfigureAwait(false);
            ioFilterInfo = await FilterPipelineMessage.Decode(driver).ConfigureAwait(false);
        }

        // checksum
        var checksum = await driver.ReadUInt32().ConfigureAwait(false);

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
    public async ValueTask<ulong> GetAddress(NativeReadContext context, ManagedObjectsFractalHeapId heapId)
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
            var (indirectBlock, entry) = await Locate(context, heapId.Offset).ConfigureAwait(false);

            /* Set direct block info */
            directBlockSize = RowBlockSizes[entry / TableWidth];
            directBlockAddress = indirectBlock.Entries[entry].Address;
        }

        // Cached by address: a dense by-name lookup resolves a heap ID per name comparison, and every
        // one of those walked back to the same handful of blocks.
        directBlock = await NativeCache
            .GetStructure(context, directBlockAddress, this, static (c, header) => FractalHeapDirectBlock.Decode(c, header))
            .ConfigureAwait(false);

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
    private async ValueTask<(FractalHeapIndirectBlock IndirectBlock, ulong entry)> Locate(NativeReadContext context, ulong offset)
    {
        var (row, column) = Lookup(offset);

        var indirectBlock = await GetIndirectBlock(context, RootBlockAddress, RootIndirectBlockRowsCount).ConfigureAwait(false);

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
            indirectBlock = await GetIndirectBlock(context, indirectBlockEntry.Address, nrows).ConfigureAwait(false);

            /* Look up row & column in new indirect block for object */
            (row, column) = Lookup(offset - indirectBlock.BlockOffset);

            if (row >= indirectBlock.RowCount)
                throw new Exception("Child fractal heap block must be smaller than its parent.");
        }

        entry = row * TableWidth + column;

        return (indirectBlock, entry);
    }

    // Cached by address, for the same reason as the direct block in GetAddress. The row count is
    // determined by the address in a well-formed heap, so it is not part of the key.
    private ValueTask<FractalHeapIndirectBlock> GetIndirectBlock(NativeReadContext context, ulong address, uint rowCount)
    {
        return NativeCache.GetStructure(
            context,
            address,
            (Header: this, RowCount: rowCount),
            static (c, state) => FractalHeapIndirectBlock.Decode(c, state.Header, state.RowCount));
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