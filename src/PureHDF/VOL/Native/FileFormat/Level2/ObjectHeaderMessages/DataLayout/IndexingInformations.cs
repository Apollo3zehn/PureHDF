namespace PureHDF.VOL.Native;

internal abstract record class IndexingInformation();


internal record class SingleChunkIndexingInformation(
    ulong FilteredChunkSize,
    uint ChunkFilters
) : IndexingInformation
{
    public static SingleChunkIndexingInformation Decode(NativeContext context, ChunkedStoragePropertyFlags flags)
    {
        var filteredChunkSize = default(ulong);
        var chunkFilters = default(uint);

        if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
        {
            var (driver, superblock) = context;

            // filtered chunk size
            filteredChunkSize = superblock.ReadLength(driver);

            // chunk filters
            chunkFilters = driver.ReadUInt32();
        }

        return new SingleChunkIndexingInformation(
            FilteredChunkSize: filteredChunkSize,
            ChunkFilters: chunkFilters
        );
    }
}

internal record class ImplicitIndexingInformation : IndexingInformation;

internal record class FixedArrayIndexingInformation(
    byte PageBits
) : IndexingInformation
{
    public static FixedArrayIndexingInformation Decode(H5DriverBase driver)
    {
        var pageBits = driver.ReadByte();

        if (pageBits == 0)
            throw new Exception("Invalid fixed array creation parameter.");

        return new FixedArrayIndexingInformation(
            PageBits: pageBits
        );
    }
}

internal record class ExtensibleArrayIndexingInformation(
    byte MaxBitCount,
    byte IndexElementsCount,
    byte MinPointerCount,
    byte MinElementsCount,
    ushort PageBitCount
) : IndexingInformation
{
    public static ExtensibleArrayIndexingInformation Decode(H5DriverBase driver)
    {
        // max bit count
        var maxBitCount = driver.ReadByte();

        if (maxBitCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // index element count
        var indexElementsCount = driver.ReadByte();

        if (indexElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min pointer count
        var minPointerCount = driver.ReadByte();

        if (minPointerCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min element count
        var minElementsCount = driver.ReadByte();

        if (minElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // page bit count
        var pageBitCount = driver.ReadByte();

        if (pageBitCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        return new ExtensibleArrayIndexingInformation(
            MaxBitCount: maxBitCount,
            IndexElementsCount: indexElementsCount,
            MinPointerCount: minPointerCount,
            MinElementsCount: minElementsCount,
            PageBitCount: pageBitCount
        );
    }
}

internal record class BTree2IndexingInformation(
    uint NodeSize,
    byte SplitPercent,
    byte MergePercent
) : IndexingInformation
{
    public static BTree2IndexingInformation Decode(H5DriverBase driver)
    {
        return new BTree2IndexingInformation(
            NodeSize: driver.ReadUInt32(),
            SplitPercent: driver.ReadByte(),
            MergePercent: driver.ReadByte()
        );
    }
}