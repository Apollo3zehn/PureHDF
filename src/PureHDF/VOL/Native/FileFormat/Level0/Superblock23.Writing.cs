using System.Buffers;

namespace PureHDF.VOL.Native;

internal partial record class Superblock23
{
    public const int ENCODE_SIZE =
        8 +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(ulong) +
        sizeof(ulong) +
        sizeof(ulong) +
        sizeof(ulong) +
        sizeof(uint);

    public async ValueTask Encode(H5DriverBase driver)
    {
        var position = driver.Position;

        driver.Write(Signature);
        driver.Write(Version);
        driver.Write(OffsetsSize);
        driver.Write(LengthsSize);
        driver.Write((byte)FileConsistencyFlags);
        driver.Write(BaseAddress);
        driver.Write(ExtensionAddress);
        driver.Write(EndOfFileAddress);
        driver.Write(RootGroupObjectHeaderAddress);

        // checksum
        driver.Seek(position, SeekOrigin.Begin);
        var checksumSize = ENCODE_SIZE - sizeof(int);
        using var owner = MemoryPool<byte>.Shared.Rent(checksumSize);
        var checksumData = owner.Memory[..checksumSize];
        await driver.Read(checksumData).ConfigureAwait(false);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData.Span);

        // The read above is async now, which leaves a BufferedFileStream's read buffer in a state
        // where the sync Write below triggers an internal FlushRead + relative seek that can fail.
        // Seeking absolutely to the write position first keeps the stream consistent.
        driver.Seek(position + checksumSize, SeekOrigin.Begin);

        driver.Write(checksum);
    }
}