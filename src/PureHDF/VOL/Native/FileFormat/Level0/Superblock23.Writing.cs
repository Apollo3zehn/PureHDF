using PureHDF.VFD;

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
        ChecksummedStructure.Write(driver, ENCODE_SIZE, driver =>
        {
            driver.Write(Signature);
            driver.Write(Version);
            driver.Write(OffsetsSize);
            driver.Write(LengthsSize);
            driver.Write((byte)FileConsistencyFlags);
            driver.Write(BaseAddress);
            driver.Write(ExtensionAddress);
            driver.Write(EndOfFileAddress);
            driver.Write(RootGroupObjectHeaderAddress);
        });
    }
}
