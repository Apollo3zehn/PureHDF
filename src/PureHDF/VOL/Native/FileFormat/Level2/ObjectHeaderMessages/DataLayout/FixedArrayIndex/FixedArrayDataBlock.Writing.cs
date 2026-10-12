using PureHDF.VFD;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayDataBlock<T>
{
    public ulong GetEncodeSize(ulong pageCount, ulong pageBitmapSize, byte entrySize)
        => GetEncodeSize((ulong)Elements.Length, pageCount, pageBitmapSize, entrySize);

    /// <summary>
    /// The size of a data block holding <paramref name="elementCount" /> elements, known before any of
    /// them is - which is what lets it be allocated with the index header rather than when the
    /// elements are finally written.
    /// </summary>
    public static ulong GetEncodeSize(ulong elementCount, ulong pageCount, ulong pageBitmapSize, byte entrySize)
    {
        var encodeSize =
            4UL +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ulong) +
            (pageCount > 0
                ? pageBitmapSize
                : elementCount * entrySize) +
            sizeof(uint);

        return encodeSize;
    }

    /// <param name="driver">Positioned where the data block goes.</param>
    /// <param name="encodeSize">From <see cref="GetEncodeSize(ulong, ulong, byte)" />, checksum included.</param>
    /// <param name="encode">Encodes one element.</param>
    internal void Encode(
        H5DriverBase driver,
        int encodeSize,
        Action<H5DriverBase, T> encode)
    {
        ChecksummedStructure.Write(driver, encodeSize, driver =>
        {
            // signature
            driver.Write(Signature);

            // version
            driver.Write(Version);

            // Client ID
            driver.Write(ClientID);

            // Header Address
            driver.Write(HeaderAddress);

            // Page Bitmap
            if (PageCount > 0)
            {
                throw new NotImplementedException();
            }

            // Elements
            else
            {
                foreach (var element in Elements)
                {
                    encode(driver, element);
                }
            }
        });
    }
}
