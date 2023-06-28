using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2(
    ulong Address, 
    ObjectHeaderFlags Flags,
    uint AccessTime,
    uint ModificationTime,
    uint ChangeTime,
    uint BirthTime,
    ushort MaximumCompactAttributesCount,
    ushort MinimumDenseAttributesCount,
    ulong SizeOfChunk0,
    List<HeaderMessage> HeaderMessages
) : ObjectHeader(Address, HeaderMessages)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OHDR");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 2)
                throw new FormatException($"Only version 2 instances of type {nameof(ObjectHeader2)} are supported.");

            _version = value;
        }
    }

    internal static ObjectHeader2 Decode(NativeContext context, byte version)
    {
        var driver = context.Driver;

        // address
        var address = (ulong)context.Driver.Position;

        // flags
        var flags = (ObjectHeaderFlags)driver.ReadByte();

        // access time, modification time, change time and birth time
        var accessTime = default(uint);
        var modificationTime = default(uint);
        var changeTime = default(uint);
        var birthTime = default(uint);

        if (flags.HasFlag(ObjectHeaderFlags.StoreFileAccessTimes))
        {
            accessTime = driver.ReadUInt32();
            modificationTime = driver.ReadUInt32();
            changeTime = driver.ReadUInt32();
            birthTime = driver.ReadUInt32();
        }

        // maximum compact attributes count and minimum dense attributes count
        var maximumCompactAttributesCount = default(ushort);
        var minimumDenseAttributesCount = default(ushort);

        if (flags.HasFlag(ObjectHeaderFlags.StoreNonDefaultAttributePhaseChangeValues))
        {
            maximumCompactAttributesCount = driver.ReadUInt16();
            minimumDenseAttributesCount = driver.ReadUInt16();
        }

        // size of chunk 0
        var chunkFieldSize = (byte)(1 << ((byte)flags & 0x03));
        var sizeOfChunk0 = Utils.ReadUlong(driver, chunkFieldSize);

        // with creation order
        var withCreationOrder = flags.HasFlag(ObjectHeaderFlags.TrackAttributeCreationOrder);

        // TODO: H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
        // TODO: read gap and checksum

        // header messages
        var headerMessages = ReadHeaderMessages(
            context, 
            address,
            sizeOfChunk0,
            version: 2, 
            withCreationOrder);

        var objectHeader = new ObjectHeader2(
            address,
            flags,
            accessTime,
            modificationTime,
            changeTime,
            birthTime,
            maximumCompactAttributesCount,
            minimumDenseAttributesCount,
            sizeOfChunk0,
            headerMessages
        )
        {
            Version = version
        };

        return objectHeader;
    }
}