namespace PureHDF.VOL.Native;

internal partial record class FixedArrayHeader
{
    public const int ENCODE_SIZE = 
        8 + 
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(byte) +
        sizeof(ulong) +
        sizeof(ulong) +
        sizeof(uint);

    internal void Encode(H5DriverBase driver)
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
        Span<byte> checksumData = stackalloc byte[ENCODE_SIZE - sizeof(int)];
        driver.Read(checksumData);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);
    }
}