namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    internal void Encode(BinaryWriter driver)
    {
        driver.Write(Signature);
        driver.Write(Version);
        driver.Write((byte)Flags);
        driver.Write(SizeOfChunk0);

        
    }
}