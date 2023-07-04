namespace PureHDF.VOL.Native;

internal readonly partial record struct HeaderMessage
{
    public void Encode(
        BinaryWriter driver,
        bool withCreationOrder)
    {
        // message type
        if (Version == 1)
            driver.Write((ushort)Type);

        else if (Version == 2)
            driver.Write((byte)Type);

        // data size (fake)
        var position1 = driver.BaseStream.Position;
        driver.Write((ushort)0 /* dummy */);

        // flags
        driver.Write((byte)Flags);

        // reserved / creation order
        if (Version == 1)
            driver.Seek(3, SeekOrigin.Current);

        else if (Version == 2 && withCreationOrder)
            driver.Write(CreationOrder);

        // data
        var position2 = driver.BaseStream.Position;
        Data.Encode(driver);
        var position3 = driver.BaseStream.Position;

        // data size (real)
        driver.BaseStream.Seek(position1, SeekOrigin.Begin);
        var dataSize = (ushort)(position3 - position2);
        driver.Write(dataSize);
        driver.BaseStream.Seek(position3, SeekOrigin.Begin);
    }
}