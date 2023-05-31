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
    public static FamilyDriverInfo Decode(H5DriverBase driver)
    {
        var _ = driver.ReadUInt64();

        return new FamilyDriverInfo();
    }
}

internal record class MultiDriverInfo() : DriverInfo
{
    public static MultiDriverInfo Decode(H5DriverBase driver)
    {
        // member mapping
        var memberMapping1 = (MemberMapping)driver.ReadByte();
        var memberMapping2 = (MemberMapping)driver.ReadByte();
        var memberMapping3 = (MemberMapping)driver.ReadByte();
        var memberMapping4 = (MemberMapping)driver.ReadByte();
        var memberMapping5 = (MemberMapping)driver.ReadByte();
        var memberMapping6 = (MemberMapping)driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

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
            memberFileStartAddresses[i] = driver.ReadUInt64();
            memberFileEndAddresses[i] = driver.ReadUInt64();
        }

        // member names
        var memberNames = new List<string>(memberCount);

        for (int i = 0; i < memberCount; i++)
        {
            memberNames[i] = ReadUtils.ReadNullTerminatedString(driver, pad: true);
        }

        return new MultiDriverInfo();
    }
}

internal record struct DriverInfoBlock()
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoBlock)} are supported.");

            _version = value;
        }
    }

    public static DriverInfoBlock Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // driver info size
        var _2 = driver.ReadUInt32();

        // driver id
        var driverId = ReadUtils.ReadFixedLengthString(driver, 8);

        // driver info
        DriverInfo _3 = driverId switch
        {
            "NCSAmulti" => MultiDriverInfo.Decode(driver),
            "NCSAfami" => FamilyDriverInfo.Decode(driver),
            _ => throw new NotSupportedException($"The driver ID '{driverId}' is not supported.")
        };

        return new DriverInfoBlock()
        {
            Version = version
        };
    }
}