namespace PureHDF.VOL.Native;

internal record ObjectHeader1(
    ulong Address,
    ushort HeaderMessagesCount,
    uint ObjectReferenceCount,
    uint ObjectHeaderSize,
    List<HeaderMessage> HeaderMessages)
    : ObjectHeader(Address, HeaderMessages)
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ObjectHeader1)} are supported.");

            _version = value;
        }
    }

    internal static async ValueTask<ObjectHeader1> Decode(NativeReadContext context, byte version)
    {
        var driver = context.Driver;

        // address
        var address = (ulong)driver.Position;

        // reserved
        await driver.ReadByte().ConfigureAwait(false);

        // header messages count
        var headerMessagesCount = await driver.ReadUInt16().ConfigureAwait(false);

        // object reference count
        var objectReferenceCount = await driver.ReadUInt32().ConfigureAwait(false);

        // object header size
        var objectHeaderSize = await driver.ReadUInt32().ConfigureAwait(false);

        // header messages

        // read padding bytes that align the following message to an 8 byte boundary
        if (objectHeaderSize > 0)
            await driver.ReadBytes(4).ConfigureAwait(false);

        var headerMessages = await ReadHeaderMessages(
            context,
            address,
            objectHeaderSize,
            version: 1,
            withCreationOrder: false).ConfigureAwait(false);

        var objectHeader = new ObjectHeader1(
            address,
            headerMessagesCount,
            objectReferenceCount,
            objectHeaderSize,
            HeaderMessages: headerMessages
        )
        {
            Version = version
        };

        return objectHeader;
    }
}