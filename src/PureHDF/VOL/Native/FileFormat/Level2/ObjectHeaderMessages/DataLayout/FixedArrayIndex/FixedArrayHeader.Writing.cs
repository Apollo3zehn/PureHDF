using PureHDF.VFD;

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

    internal void Encode(H5DriverBase driver)
    {
        ChecksummedStructure.Write(driver, ENCODE_SIZE, driver =>
        {
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
        });
    }
}
