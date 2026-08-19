using System.Buffers;

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

    internal async ValueTask Encode(H5DriverBase driver)
    {
        var position = driver.Position;

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

        // Checksum
        driver.Seek(position, SeekOrigin.Begin);
        using var owner = MemoryPool<byte>.Shared.Rent(ENCODE_SIZE - sizeof(int));
        var checksumData = owner.Memory[..(ENCODE_SIZE - sizeof(int))];
        await driver.Read(checksumData).ConfigureAwait(false);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData.Span);

        // Absolute seek: see Superblock23.Encode - the async read leaves a BufferedFileStream's
        // read buffer in a state where the sync Write below can fail inside FlushRead.
        driver.Seek(position + (ENCODE_SIZE - sizeof(int)), SeekOrigin.Begin);

        driver.Write(checksum);
    }
}