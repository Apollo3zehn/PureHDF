namespace PureHDF.VOL.Native;

internal partial record class Superblock23
{
    public void Encode(BinaryWriter driver)
    {
        driver.Write(new byte[] { 0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a });
        driver.Write(Version);
        driver.Write(OffsetsSize);
        driver.Write(LengthsSize);
        driver.Write((byte)FileConsistencyFlags);
        driver.Write(BaseAddress);
        driver.Write(ExtensionAddress);
        driver.Write(EndOfFileAddress);
        driver.Write(RootGroupObjectHeaderAddress);
        driver.Write(Checksum);
    }
}