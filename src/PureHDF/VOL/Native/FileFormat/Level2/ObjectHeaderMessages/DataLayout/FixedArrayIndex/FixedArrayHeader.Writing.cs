using PureHDF.VFD;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayHeader
{
    public const int ENCODE_SIZE =
        4 +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(ulong) +
        sizeof(ulong) +
        sizeof(uint);

    /// <summary>
    /// The size of a filtered dataset's chunk index: this header, and its data block right behind it.
    /// </summary>
    internal static long GetFilteredIndexEncodeSize(byte pageBits, ulong chunkCount, ulong chunkByteSize)
        => ENCODE_SIZE + (long)GetFilteredDataBlockEncodeSize(pageBits, chunkCount, chunkByteSize);

    /// <summary>
    /// The size of a filtered dataset's index data block, known from the chunk count before any chunk is
    /// written.
    /// </summary>
    internal static ulong GetFilteredDataBlockEncodeSize(byte pageBits, ulong chunkCount, ulong chunkByteSize)
    {
        /* H5FA__cache_hdr_serialize (H5FAcache.c) */
        var entrySize = FilteredDataBlockElement.GetEncodeSize(MathUtils.ComputeChunkSizeLength(chunkByteSize));
        var (_, pageCount, pageBitmapSize) = GetPageInfo(pageBits, chunkCount);

        return FixedArrayDataBlock<FilteredDataBlockElement>.GetEncodeSize(chunkCount, pageCount, pageBitmapSize, entrySize);
    }

    internal void Encode(H5DriverBase driver)
    {
        ChecksummedStructure.Write(driver, ENCODE_SIZE, driver =>
        {
            // signature
            driver.Write(Signature);

            // version
            driver.Write(Version);

            // Client ID
            driver.Write(ClientID);

            // Entry Size
            driver.Write(EntrySize);

            // Page Bits
            driver.Write(PageBits);

            // Max Num Entries
            driver.Write(EntriesCount);

            // Data Block Address
            driver.Write(DataBlockAddress);
        });
    }
}
