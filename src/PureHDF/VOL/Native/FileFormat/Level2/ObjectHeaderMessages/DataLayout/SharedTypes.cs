namespace PureHDF.VOL.Native;

internal enum ClientID
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
    public const byte ENCODE_SIZE = 
        sizeof(ulong) +
        sizeof(uint) +
        sizeof(uint);
}

internal record struct DataBlockPage<T>(
    T[] Elements
)
{
    public static DataBlockPage<T> Decode(
        H5DriverBase driver, 
        ulong elementCount, 
        Func<H5DriverBase, T> decode)
    {
        // elements
        var elements = Enumerable
            .Range(0, (int)elementCount)
            .Select(i => decode(driver))
            .ToArray();

        // checksum
        var _ = driver.ReadUInt32();

        return new DataBlockPage<T>(
            Elements: elements
        );
    }
}