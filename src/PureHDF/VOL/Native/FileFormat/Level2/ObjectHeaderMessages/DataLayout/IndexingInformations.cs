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

    public static async ValueTask<SingleChunkIndexingInformation> Decode(NativeReadContext context, ChunkedStoragePropertyFlags flags)
    {
        var filteredChunkSize = default(ulong);
        var chunkFilters = default(uint);

        if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
        {
            var (driver, superblock) = context;

            // filtered chunk size
            filteredChunkSize = await superblock.ReadLength(driver).ConfigureAwait(false);

            // chunk filters
            chunkFilters = await driver.ReadUInt32().ConfigureAwait(false);
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
    public static async ValueTask<FixedArrayIndexingInformation> Decode(H5DriverBase driver)
    {
        var pageBits = await driver.ReadByte().ConfigureAwait(false);

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
    public static async ValueTask<ExtensibleArrayIndexingInformation> Decode(H5DriverBase driver)
    {
        // max bit count
        var maxBitCount = await driver.ReadByte().ConfigureAwait(false);

        if (maxBitCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // index element count
        var indexElementsCount = await driver.ReadByte().ConfigureAwait(false);

        if (indexElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min pointer count
        var minPointerCount = await driver.ReadByte().ConfigureAwait(false);

        if (minPointerCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min element count
        var minElementsCount = await driver.ReadByte().ConfigureAwait(false);

        if (minElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // page bit count
        var pageBitCount = await driver.ReadByte().ConfigureAwait(false);

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
    public static async ValueTask<BTree2IndexingInformation> Decode(H5DriverBase driver)
    {
        var nodeSize = await driver.ReadUInt32().ConfigureAwait(false);
        var splitPercent = await driver.ReadByte().ConfigureAwait(false);
        var mergePercent = await driver.ReadByte().ConfigureAwait(false);

        return new BTree2IndexingInformation(
            NodeSize: nodeSize,
            SplitPercent: splitPercent,
            MergePercent: mergePercent
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