using System.Buffers;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayDataBlock<T>
{
    public ulong GetEncodeSize(ulong pageCount, ulong pageBitmapSize, byte entrySize)
    {
        var encodeSize =
            8 +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ulong) +
            pageCount > 0
                ? (long)pageBitmapSize
                : Elements.Length * entrySize;

        return (ulong)encodeSize;
    }

    internal void Encode(
        H5DriverBase driver,
        Action<H5DriverBase, T> encode)
    {
        var position = driver.Position;

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

        // Checksum
        var bufferSize = (int)(driver.Position - position);
        using var buffer = MemoryPool<byte>.Shared.Rent(bufferSize);
        var checksumData = buffer.Memory.Span[..bufferSize];

        driver.Seek(position, SeekOrigin.Begin);
        driver.Read(checksumData);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);
    }
}