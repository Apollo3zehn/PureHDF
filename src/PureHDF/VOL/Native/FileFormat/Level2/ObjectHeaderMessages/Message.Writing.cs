namespace PureHDF.VOL.Native;

internal abstract partial record class Message
{
    public virtual ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public virtual void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}