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
        var dataEncodeSize = Data.GetEncodeSize();
        driver.Write(dataEncodeSize);

        // flags
        driver.Write((byte)Flags);

        // reserved / creation order
        if (Version == 1)
            driver.Seek(3, SeekOrigin.Current);

        else if (Version == 2 && withCreationOrder)
            driver.Write(CreationOrder);

        // data
        Data.Encode(driver);
    }

    public uint GetEncodeSize(bool withCreationOrder)
    {
        if (Version != 2)
            throw new Exception("Only v2 header messages are supported");

        var dataEncodeSize = Data.GetEncodeSize();

        return sizeof(byte) +
            sizeof(ushort) +
            sizeof(byte) +
            (
                withCreationOrder
                    ? sizeof(ushort)
                    : 0U
            ) +
            dataEncodeSize;
    }
}