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
    public static async ValueTask<ObjectHeaderSharedMessageRecord> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // message location
        var messageLocation = (MessageLocation)await driver.ReadByte().ConfigureAwait(false);

        // hash value
        var hashValue = await driver.ReadUInt32().ConfigureAwait(false);

        // reserved
        await driver.ReadByte().ConfigureAwait(false);

        // message type
        var messageType = (MessageType)await driver.ReadByte().ConfigureAwait(false);

        // creation index
        var creationIndex = await driver.ReadUInt16().ConfigureAwait(false);

        // object header address
        var objectHeaderAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

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
    public static async ValueTask<FractalHeapSharedMessageRecord> Decode(H5DriverBase driver)
    {
        return new FractalHeapSharedMessageRecord(
            MessageLocation: (MessageLocation)await driver.ReadByte().ConfigureAwait(false),
            HashValue: await driver.ReadUInt32().ConfigureAwait(false),
            ReferenceCount: await driver.ReadUInt32().ConfigureAwait(false),
            FractalHeapId: await driver.ReadUInt64().ConfigureAwait(false)
        );
    }
}