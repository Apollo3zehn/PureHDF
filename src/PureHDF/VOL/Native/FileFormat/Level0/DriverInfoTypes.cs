namespace PureHDF.VOL.Native;

internal enum MemberMapping : byte
{
    Superblock = 1,
    BTree = 2,
    Raw = 3,
    GlobalHeap = 4,
    LocalHeap = 5,
    ObjectHeader = 6
}

internal abstract record class DriverInfo();

internal record class FamilyDriverInfo() : DriverInfo
{
    public static async ValueTask<FamilyDriverInfo> Decode(H5DriverBase driver)
    {
        var _ = await driver.ReadUInt64().ConfigureAwait(false);

        return new FamilyDriverInfo();
    }
}

internal record class MultiDriverInfo() : DriverInfo
{
    public static async ValueTask<MultiDriverInfo> Decode(H5DriverBase driver)
    {
        // member mapping
        var memberMapping1 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);
        var memberMapping2 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);
        var memberMapping3 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);
        var memberMapping4 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);
        var memberMapping5 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);
        var memberMapping6 = (MemberMapping)await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(3).ConfigureAwait(false);

        // member count
        var memberCount = new MemberMapping[] {
            memberMapping1, memberMapping2, memberMapping3,
            memberMapping4, memberMapping5, memberMapping6
        }.Distinct().Count();

        // member start and end addresses
        var memberFileStartAddresses = new List<ulong>(memberCount);
        var memberFileEndAddresses = new List<ulong>(memberCount);

        for (int i = 0; i < memberCount; i++)
        {
            memberFileStartAddresses[i] = await driver.ReadUInt64().ConfigureAwait(false);
            memberFileEndAddresses[i] = await driver.ReadUInt64().ConfigureAwait(false);
        }

        // member names
        var memberNames = new List<string>(memberCount);

        for (int i = 0; i < memberCount; i++)
        {
            memberNames[i] = await ReadUtils.ReadNullTerminatedString(driver, pad: true).ConfigureAwait(false);
        }

        return new MultiDriverInfo();
    }
}

internal readonly record struct DriverInfoBlock()
{
    private readonly byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoBlock)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<DriverInfoBlock> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(3).ConfigureAwait(false);

        // driver info size
        var _1 = await driver.ReadUInt32().ConfigureAwait(false);

        // driver id
        var driverId = await ReadUtils.ReadFixedLengthString(driver, 8).ConfigureAwait(false);

        // driver info
        DriverInfo _2 = driverId switch
        {
            "NCSAmulti" => await MultiDriverInfo.Decode(driver).ConfigureAwait(false),
            "NCSAfami" => await FamilyDriverInfo.Decode(driver).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The driver ID '{driverId}' is not supported.")
        };

        return new DriverInfoBlock()
        {
            Version = version
        };
    }
}