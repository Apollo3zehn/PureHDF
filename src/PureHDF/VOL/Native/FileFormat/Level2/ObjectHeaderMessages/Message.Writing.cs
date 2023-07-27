namespace PureHDF.VOL.Native;

internal abstract partial record class Message
{
    public virtual ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public virtual void Encode(BinaryWriter driver)
    {
        throw new NotImplementedException();
    }
}