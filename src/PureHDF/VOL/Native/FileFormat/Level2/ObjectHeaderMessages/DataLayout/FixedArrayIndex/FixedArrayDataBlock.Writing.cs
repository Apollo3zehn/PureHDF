using System.Buffers;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayDataBlock<T>
{
    public ulong GetEncodeSize(ulong pageCount, ulong pageBitmapSize, byte entrySize)
    {
        var encodeSize =
            4 +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ulong) +
            (pageCount > 0
                ? (long)pageBitmapSize
                : Elements.Length * entrySize) +
            sizeof(uint);

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
        var checksumData = buffer.Memory[..bufferSize];

        driver.Seek(position, SeekOrigin.Begin);

        // SYNC SURFACE: Encode is synchronous (the writer is), while driver.Read is async. The
        // ValueTask must be waited on rather than discarded - discarding it draws no CS4014 warning,
        // because the enclosing method is not async, and computes the checksum over uninitialized
        // pooled memory. AsTask() is required: blocking directly on an IValueTaskSource-backed
        // ValueTask is not supported and throws.
        driver.Read(checksumData).AsTask().GetAwaiter().GetResult();

        var checksum = ChecksumUtils.JenkinsLookup3(checksumData.Span);

        // Absolute seek: the async read leaves a BufferedFileStream's read buffer in a state where
        // the sync Write below can fail inside its internal FlushRead.
        driver.Seek(position + bufferSize, SeekOrigin.Begin);

        driver.Write(checksum);
    }
}