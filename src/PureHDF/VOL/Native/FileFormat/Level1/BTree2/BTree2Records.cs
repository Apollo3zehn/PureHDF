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
    public static BTree2Record01 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new BTree2Record01(
            HugeObjectAddress: superblock.ReadOffset(driver),
            HugeObjectLength: superblock.ReadLength(driver),
            HugeObjectId: superblock.ReadLength(driver)
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
    public static BTree2Record02 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new BTree2Record02(
            FilteredHugeObjectAddress: superblock.ReadOffset(driver),
            FilteredHugeObjectLength: superblock.ReadLength(driver),
            FilterMask: driver.ReadUInt32(),
            FilteredHugeObjectMemorySize: superblock.ReadLength(driver),
            HugeObjectId: superblock.ReadLength(driver)
        );
    }
}

internal readonly record struct BTree2Record03(
    ulong HugeObjectAddress,
    ulong HugeObjectLength
) : IBTree2Record
{
    public static BTree2Record03 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new BTree2Record03(
            HugeObjectAddress: superblock.ReadOffset(driver),
            HugeObjectLength: superblock.ReadLength(driver)
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
    public static BTree2Record04 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new BTree2Record04(
            FilteredHugeObjectAddress: superblock.ReadOffset(driver),
            FilteredHugeObjectLength: superblock.ReadLength(driver),
            FilterMask: driver.ReadUInt32(),
            FilteredHugeObjectMemorySize: superblock.ReadLength(driver)
        );
    }
}

internal readonly record struct BTree2Record05(
    uint NameHash,
    byte[] HeapId
) : IBTree2Record
{
    public static BTree2Record05 Decode(H5DriverBase driver)
    {
        return new BTree2Record05(
            NameHash: driver.ReadUInt32(),
            HeapId: driver.ReadBytes(7)
        );
    }
}

internal readonly record struct BTree2Record06(
    ulong CreationOrder,
    byte[] HeapId
) : IBTree2Record
{
    public static BTree2Record06 Decode(H5DriverBase driver)
    {
        return new BTree2Record06(
            CreationOrder: driver.ReadUInt64(),
            HeapId: driver.ReadBytes(7)
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
    public static BTree2Record08 Decode(H5DriverBase driver)
    {
        return new BTree2Record08(
            HeapId: driver.ReadBytes(8),
            MessageFlags: (MessageFlags)driver.ReadByte(),
            CreationOrder: driver.ReadUInt32(),
            NameHash: driver.ReadUInt32()
        );
    }
}

internal readonly record struct BTree2Record09(
    byte[] HeapId,
    MessageFlags MessageFlags,
    uint CreationOrder
) : IBTree2Record
{
    public static BTree2Record09 Decode(H5DriverBase driver)
    {
        return new BTree2Record09(
            HeapId: driver.ReadBytes(8),
            MessageFlags: (MessageFlags)driver.ReadByte(),
            CreationOrder: driver.ReadUInt32()
        );
    }
}

internal readonly record struct BTree2Record10(
    ulong Address,
    ulong[] ScaledOffsets
) : IBTree2Record
{
    public static BTree2Record10 Decode(NativeReadContext context, byte rank)
    {
        var (driver, superblock) = context;

        // address
        var address = superblock.ReadOffset(driver);

        // scaled offsets
        var scaledOffsets = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            scaledOffsets[i] = driver.ReadUInt64();
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
    public static BTree2Record11 Decode(NativeReadContext context, byte rank, uint chunkSizeLength)
    {
        var (driver, superblock) = context;

        // address
        var address = superblock.ReadOffset(driver);

        // chunk size
        var chunkSize = ReadUtils.ReadUlong(driver, chunkSizeLength);

        // filter mask
        var filterMask = driver.ReadUInt32();

        // scaled offsets
        var scaledOffsets = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            scaledOffsets[i] = driver.ReadUInt64();
        }

        return new BTree2Record11(
            Address: address,
            ChunkSize: chunkSize,
            FilterMask: filterMask,
            ScaledOffsets: scaledOffsets
        );
    }
}