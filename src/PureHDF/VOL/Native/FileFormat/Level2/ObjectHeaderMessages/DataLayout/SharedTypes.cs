namespace PureHDF.VOL.Native;

internal enum ClientID : byte
{
    NonFilteredDatasetChunks = 0,
    FilteredDatasetChunks = 1
}

internal record class DataBlockElement(
    ulong Address
);

internal record class FilteredDataBlockElement(
    ulong Address,
    uint ChunkSize,
    uint FilterMask
) : DataBlockElement(Address)
{
    public static byte GetEncodeSize(uint chunkSizeLength)
    {
        var encodeSize =
            sizeof(ulong) +
            chunkSizeLength +
            sizeof(uint);

        return (byte)encodeSize;
    }
}

internal record struct DataBlockPage<T>(
    T[] Elements
)
{
    public static async ValueTask<DataBlockPage<T>> Decode(
        H5DriverBase driver,
        ulong elementCount,
        Func<H5DriverBase, ValueTask<T>> decode)
    {
        // elements
        // An explicit loop rather than Enumerable.Range(...).Select(...).ToArray(): a lambda cannot
        // be awaited inside Select, and these reads are strictly sequential.
        var elements = new T[(int)elementCount];

        for (var i = 0; i < elements.Length; i++)
        {
            elements[i] = await decode(driver).ConfigureAwait(false);
        }

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new DataBlockPage<T>(
            Elements: elements
        );
    }
}