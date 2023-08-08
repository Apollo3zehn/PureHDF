namespace PureHDF.VOL.Native;

internal partial record class FixedArrayHeader
{
    public const long ENCODE_SIZE = 
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
        throw new NotImplementedException();
    }
}