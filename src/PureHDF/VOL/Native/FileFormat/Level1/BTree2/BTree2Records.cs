namespace PureHDF.VOL.Native;

internal interface IBTree2Record
{
    //
}

internal readonly record struct BTree2Record01(
    ulong HugeObjectAddress,
    ulong HugeObjectLength,
    ulong HugeObjectId
) : IBTree2Record
{
    public static async ValueTask<BTree2Record01> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var hugeObjectAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var hugeObjectLength = await superblock.ReadLength(driver).ConfigureAwait(false);
        var hugeObjectId = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new BTree2Record01(
            HugeObjectAddress: hugeObjectAddress,
            HugeObjectLength: hugeObjectLength,
            HugeObjectId: hugeObjectId
        );
    }
}

internal readonly record struct BTree2Record02(
    ulong FilteredHugeObjectAddress,
    ulong FilteredHugeObjectLength,
    uint FilterMask,
    ulong FilteredHugeObjectMemorySize,
    ulong HugeObjectId
) : IBTree2Record
{
    public static async ValueTask<BTree2Record02> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var filteredHugeObjectAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var filteredHugeObjectLength = await superblock.ReadLength(driver).ConfigureAwait(false);
        var filterMask = await driver.ReadUInt32().ConfigureAwait(false);
        var filteredHugeObjectMemorySize = await superblock.ReadLength(driver).ConfigureAwait(false);
        var hugeObjectId = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new BTree2Record02(
            FilteredHugeObjectAddress: filteredHugeObjectAddress,
            FilteredHugeObjectLength: filteredHugeObjectLength,
            FilterMask: filterMask,
            FilteredHugeObjectMemorySize: filteredHugeObjectMemorySize,
            HugeObjectId: hugeObjectId
        );
    }
}

internal readonly record struct BTree2Record03(
    ulong HugeObjectAddress,
    ulong HugeObjectLength
) : IBTree2Record
{
    public static async ValueTask<BTree2Record03> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var hugeObjectAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var hugeObjectLength = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new BTree2Record03(
            HugeObjectAddress: hugeObjectAddress,
            HugeObjectLength: hugeObjectLength
        );
    }
}

internal readonly record struct BTree2Record04(
    ulong FilteredHugeObjectAddress,
    ulong FilteredHugeObjectLength,
    uint FilterMask,
    ulong FilteredHugeObjectMemorySize
) : IBTree2Record
{
    public static async ValueTask<BTree2Record04> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var filteredHugeObjectAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var filteredHugeObjectLength = await superblock.ReadLength(driver).ConfigureAwait(false);
        var filterMask = await driver.ReadUInt32().ConfigureAwait(false);
        var filteredHugeObjectMemorySize = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new BTree2Record04(
            FilteredHugeObjectAddress: filteredHugeObjectAddress,
            FilteredHugeObjectLength: filteredHugeObjectLength,
            FilterMask: filterMask,
            FilteredHugeObjectMemorySize: filteredHugeObjectMemorySize
        );
    }
}

internal readonly record struct BTree2Record05(
    uint NameHash,
    byte[] HeapId
) : IBTree2Record
{
    public static async ValueTask<BTree2Record05> Decode(H5DriverBase driver)
    {
        var nameHash = await driver.ReadUInt32().ConfigureAwait(false);
        var heapId = await driver.ReadBytes(7).ConfigureAwait(false);

        return new BTree2Record05(
            NameHash: nameHash,
            HeapId: heapId
        );
    }
}

internal readonly record struct BTree2Record06(
    ulong CreationOrder,
    byte[] HeapId
) : IBTree2Record
{
    public static async ValueTask<BTree2Record06> Decode(H5DriverBase driver)
    {
        var creationOrder = await driver.ReadUInt64().ConfigureAwait(false);
        var heapId = await driver.ReadBytes(7).ConfigureAwait(false);

        return new BTree2Record06(
            CreationOrder: creationOrder,
            HeapId: heapId
        );
    }
}

// internal readonly record struct BTree2Record07(
//     byte[] Hash,
//     uint ReferenceCount,
//     byte[] HeapId
// ) : IBTree2Record
// {
//     public static BTree2Record07 Construct(NativeContext context)
//     {
//         var (driver, superblock) = context;

//         var messageLocation = (MessageLocation)driver.ReadByte();

//         return messageLocation switch
//         {
//             MessageLocation.Heap            => BTree2Record07_0.Decode(driver),
//             MessageLocation.ObjectHeader    => BTree2Record07_1.Decode(context),
//             _                               => throw new Exception($"Unknown message location '{MessageLocation.Heap}'.")
//         };
//     }
// }

// internal readonly record struct BTree2Record07_0(
//     byte[] Hash,
//     uint ReferenceCount,
//     byte[] HeapId
// ) : BTree2Record07
// {
//     public static BTree2Record07_0 Decode(H5DriverBase driver)
//     {
//         return new BTree2Record07_0(
//             Hash: driver.ReadBytes(4),
//             ReferenceCount: driver.ReadUInt32(),
//             HeapId: driver.ReadBytes(8)
//         );
//     }
// }

// internal readonly record struct BTree2Record07_1(
//     byte[] Hash,
//     HeaderMessageType MessageType,
//     ushort HeaderIndex,
//     ulong HeaderAddress
// ) : BTree2Record07
// {
//     public static BTree2Record07_1 Decode(NativeContext context)
//     {
//         var (driver, superblock) = context;

//         var hash = driver.ReadBytes(4);

//         // reserved
//         driver.ReadByte();

//         return new BTree2Record07_1(
//             Hash: hash,
//             MessageType: (HeaderMessageType)driver.ReadByte(),
//             HeaderIndex: driver.ReadUInt16(),
//             HeaderAddress: superblock.ReadOffset(driver)
//         );
//     }
// }

internal readonly record struct BTree2Record08(
    byte[] HeapId,
    MessageFlags MessageFlags,
    uint CreationOrder,
    uint NameHash
) : IBTree2Record
{
    public static async ValueTask<BTree2Record08> Decode(H5DriverBase driver)
    {
        var heapId = await driver.ReadBytes(8).ConfigureAwait(false);
        var messageFlags = (MessageFlags)(await driver.ReadByte().ConfigureAwait(false));
        var creationOrder = await driver.ReadUInt32().ConfigureAwait(false);
        var nameHash = await driver.ReadUInt32().ConfigureAwait(false);

        return new BTree2Record08(
            HeapId: heapId,
            MessageFlags: messageFlags,
            CreationOrder: creationOrder,
            NameHash: nameHash
        );
    }
}

internal readonly record struct BTree2Record09(
    byte[] HeapId,
    MessageFlags MessageFlags,
    uint CreationOrder
) : IBTree2Record
{
    public static async ValueTask<BTree2Record09> Decode(H5DriverBase driver)
    {
        var heapId = await driver.ReadBytes(8).ConfigureAwait(false);
        var messageFlags = (MessageFlags)(await driver.ReadByte().ConfigureAwait(false));
        var creationOrder = await driver.ReadUInt32().ConfigureAwait(false);

        return new BTree2Record09(
            HeapId: heapId,
            MessageFlags: messageFlags,
            CreationOrder: creationOrder
        );
    }
}

internal readonly record struct BTree2Record10(
    ulong Address,
    ulong[] ScaledOffsets
) : IBTree2Record
{
    public static async ValueTask<BTree2Record10> Decode(NativeReadContext context, byte rank)
    {
        var (driver, superblock) = context;

        // address
        var address = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // scaled offsets
        var scaledOffsets = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            scaledOffsets[i] = await driver.ReadUInt64().ConfigureAwait(false);
        }

        return new BTree2Record10(
            Address: address,
            ScaledOffsets: scaledOffsets
        );
    }
}

internal readonly record struct BTree2Record11(
    ulong Address,
    ulong ChunkSize,
    uint FilterMask,
    ulong[] ScaledOffsets
) : IBTree2Record
{
    public static async ValueTask<BTree2Record11> Decode(NativeReadContext context, byte rank, uint chunkSizeLength)
    {
        var (driver, superblock) = context;

        // address
        var address = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // chunk size
        var chunkSize = await ReadUtils.ReadUlong(driver, chunkSizeLength).ConfigureAwait(false);

        // filter mask
        var filterMask = await driver.ReadUInt32().ConfigureAwait(false);

        // scaled offsets
        var scaledOffsets = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            scaledOffsets[i] = await driver.ReadUInt64().ConfigureAwait(false);
        }

        return new BTree2Record11(
            Address: address,
            ChunkSize: chunkSize,
            FilterMask: filterMask,
            ScaledOffsets: scaledOffsets
        );
    }
}
