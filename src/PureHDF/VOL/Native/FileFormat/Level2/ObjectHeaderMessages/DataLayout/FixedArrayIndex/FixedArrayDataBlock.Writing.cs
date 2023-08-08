namespace PureHDF.VOL.Native;

internal partial record class FixedArrayDataBlock<T>
{
    public ulong GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    internal void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}