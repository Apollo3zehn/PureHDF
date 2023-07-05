namespace PureHDF.VOL.Native;

internal partial record class Superblock23
{
    public const int SIZE = 8 + 4 + 8 + 8 + 8 + 8 + 4;

    public void Encode(BinaryWriter driver)
    {
        var position = driver.BaseStream.Position;

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
        driver.BaseStream.Seek(position, SeekOrigin.Begin);
        Span<byte> checksumData = stackalloc byte[SIZE - 4];
        driver.BaseStream.Read(checksumData);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);
    }
}