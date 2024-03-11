namespace PureHDF.VOL.Native;

internal abstract record class IndexingInformation()
{
    public abstract ushort GetEncodeSize(ChunkedStoragePropertyFlags flags);

    public abstract void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags);
};


internal record class SingleChunkIndexingInformation(
    uint ChunkFilters
) : IndexingInformation
{
    public ulong FilteredChunkSize { get; set; }

    public static SingleChunkIndexingInformation Decode(NativeReadContext context, ChunkedStoragePropertyFlags flags)
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
            ChunkFilters: chunkFilters
        )
        {
            FilteredChunkSize = filteredChunkSize
        };
    }

    public override ushort GetEncodeSize(ChunkedStoragePropertyFlags flags)
    {
        if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
        {
            return
                sizeof(ulong) +
                sizeof(uint);
        }

        else
        {
            return 0;
        }
    }

    public override void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags)
    {
        if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
        {
            // filtered chunk size
            driver.Write(FilteredChunkSize);

            // chunk filters
            driver.Write(ChunkFilters);
        }
    }
}

internal record class ImplicitIndexingInformation : IndexingInformation
{
    public override ushort GetEncodeSize(ChunkedStoragePropertyFlags flags)
    {
        return 0;
    }

    public override void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags)
    {
        return;
    }
};

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

    public override ushort GetEncodeSize(ChunkedStoragePropertyFlags flags)
    {
        return sizeof(byte);
    }

    public override void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags)
    {
        // page bits
        driver.Write(PageBits);
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

    public override ushort GetEncodeSize(ChunkedStoragePropertyFlags flags)
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags)
    {
        throw new NotImplementedException();
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

    public override ushort GetEncodeSize(ChunkedStoragePropertyFlags flags)
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver, ChunkedStoragePropertyFlags flags)
    {
        throw new NotImplementedException();
    }
}