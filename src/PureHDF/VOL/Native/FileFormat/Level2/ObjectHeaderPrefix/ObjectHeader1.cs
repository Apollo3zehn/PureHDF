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

    internal static ObjectHeader1 Decode(NativeReadContext context, byte version)
    {
        var driver = context.Driver;

        // address
        var address = (ulong)driver.Position;

        // reserved
        driver.ReadByte();

        // header messages count
        var headerMessagesCount = driver.ReadUInt16();

        // object reference count
        var objectReferenceCount = driver.ReadUInt32();

        // object header size
        var objectHeaderSize = driver.ReadUInt32();

        // header messages

        // read padding bytes that align the following message to an 8 byte boundary
        if (objectHeaderSize > 0)
            driver.ReadBytes(4);

        var headerMessages = ReadHeaderMessages(
            context, 
            address,
            objectHeaderSize,
            version: 1,
            withCreationOrder: false);

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