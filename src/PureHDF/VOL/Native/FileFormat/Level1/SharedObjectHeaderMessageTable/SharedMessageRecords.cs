namespace PureHDF.VOL.Native;

internal abstract record class SharedMessageRecord(
    MessageLocation MessageLocation
)
{
    //
}

internal record class ObjectHeaderSharedMessageRecord(
    MessageLocation MessageLocation,
    uint HashValue,
    MessageType MessageType,
    ushort CreationIndex,
    ulong ObjectHeaderAddress
) : SharedMessageRecord(MessageLocation)
{
    public static ObjectHeaderSharedMessageRecord Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // message location
        var messageLocation = (MessageLocation)driver.ReadByte();

        // hash value
        var hashValue = driver.ReadUInt32();

        // reserved
        driver.ReadByte();

        // message type
        var messageType = (MessageType)driver.ReadByte();

        // creation index
        var creationIndex = driver.ReadUInt16();

        // object header address
        var objectHeaderAddress = superblock.ReadOffset(driver);

        return new ObjectHeaderSharedMessageRecord(
            MessageLocation: messageLocation,
            HashValue: hashValue,
            MessageType: messageType,
            CreationIndex: creationIndex,
            ObjectHeaderAddress: objectHeaderAddress
        );
    }
}

internal record class FractalHeapSharedMessageRecord(
    MessageLocation MessageLocation,
    uint HashValue,
    uint ReferenceCount,
    ulong FractalHeapId
) : SharedMessageRecord(MessageLocation)
{
    public static FractalHeapSharedMessageRecord Decode(H5DriverBase driver)
    {
        return new FractalHeapSharedMessageRecord(
            MessageLocation: (MessageLocation)driver.ReadByte(),
            HashValue: driver.ReadUInt32(),
            ReferenceCount: driver.ReadUInt32(),
            FractalHeapId: driver.ReadUInt64()
        );
    }
}