namespace PureHDF.VOL.Native;

internal abstract record class StoragePropertyDescription(
    ulong Address
);

internal record class CompactStoragePropertyDescription(
    byte[] RawData
) : StoragePropertyDescription(Superblock.UndefinedAddress)
{
    public static CompactStoragePropertyDescription Decode(H5DriverBase driver)
    {
        var size = driver.ReadUInt16();

        return new CompactStoragePropertyDescription(
            RawData: driver.ReadBytes(size)
        );
    }
}

internal record class ContiguousStoragePropertyDescription(
    ulong Address,
    ulong Size
) : StoragePropertyDescription(Address)
{
    public static ContiguousStoragePropertyDescription Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new ContiguousStoragePropertyDescription(
            Address: superblock.ReadOffset(driver),
            Size: superblock.ReadLength(driver)
        );
    }
}

internal abstract record class ChunkedStoragePropertyDescription(
    ulong Address,
    byte Rank
) : StoragePropertyDescription(Address);

internal record class ChunkedStoragePropertyDescription3(
    ulong Address,
    byte Rank,
    uint[] DimensionSizes
) : ChunkedStoragePropertyDescription(Address, Rank)
{
    public static ChunkedStoragePropertyDescription3 Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // rank
        var rank = driver.ReadByte();

        // address
        var address = superblock.ReadOffset(driver);

        // dimension sizes
        var dimensionSizes = new uint[rank];

        for (uint i = 0; i < rank; i++)
        {
            dimensionSizes[i] = driver.ReadUInt32();
        }

        return new ChunkedStoragePropertyDescription3(
            Address: address,
            Rank: rank,
            DimensionSizes: dimensionSizes
        );
    }
}

internal record class ChunkedStoragePropertyDescription4(
    ulong Address,
    byte Rank,
    ChunkedStoragePropertyFlags Flags,
    ulong[] DimensionSizes,
    ChunkIndexingType ChunkIndexingType,
    IndexingInformation IndexingTypeInformation
) : ChunkedStoragePropertyDescription(Address, Rank)
{
    public static ChunkedStoragePropertyDescription4 Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // flags
        var flags = (ChunkedStoragePropertyFlags)driver.ReadByte();

        // rank
        var rank = driver.ReadByte();

        // dimension size encoded length
        var dimensionSizeEncodedLength = driver.ReadByte();

        // dimension sizes
        var dimensionSizes = new ulong[rank];

        for (uint i = 0; i < rank; i++)
        {
            dimensionSizes[i] = Utils.ReadUlong(driver, dimensionSizeEncodedLength);
        }

        // chunk indexing type
        var chunkIndexingType = (ChunkIndexingType)driver.ReadByte();

        // indexing type information
        IndexingInformation indexingTypeInformation = chunkIndexingType switch
        {
            ChunkIndexingType.SingleChunk => SingleChunkIndexingInformation.Decode(context, flags),
            ChunkIndexingType.Implicit => new ImplicitIndexingInformation(),
            ChunkIndexingType.FixedArray => FixedArrayIndexingInformation.Decode(driver),
            ChunkIndexingType.ExtensibleArray => ExtensibleArrayIndexingInformation.Decode(driver),
            ChunkIndexingType.BTree2 => BTree2IndexingInformation.Decode(driver),
            _ => throw new NotSupportedException($"The chunk indexing type '{chunkIndexingType}' is not supported.")
        };

        // address
        var address = superblock.ReadOffset(driver);

        return new ChunkedStoragePropertyDescription4(
            Address: address,
            Rank: rank,
            Flags: flags,
            DimensionSizes: dimensionSizes,
            ChunkIndexingType: chunkIndexingType,
            IndexingTypeInformation: indexingTypeInformation
        );
    }
}

internal record class VirtualStoragePropertyDescription(
    ulong Address,
    uint Index
) : StoragePropertyDescription(Address)
{
    public static VirtualStoragePropertyDescription Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new VirtualStoragePropertyDescription(
            Address: superblock.ReadOffset(driver),
            Index: driver.ReadUInt32()
        );
    }
}