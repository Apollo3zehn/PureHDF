namespace PureHDF.VOL.Native;

internal abstract partial record class Message
{
    public virtual void Encode(BinaryWriter driver)
    {
        throw new NotImplementedException();
    }

    public ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }
}