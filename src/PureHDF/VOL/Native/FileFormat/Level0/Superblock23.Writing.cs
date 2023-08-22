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

    public void Encode(H5DriverBase driver)
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
        Span<byte> checksumData = stackalloc byte[ENCODE_SIZE - sizeof(int)];
        driver.Read(checksumData);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);
    }
}