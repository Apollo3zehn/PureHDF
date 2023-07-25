namespace PureHDF.VOL.Native;

internal abstract record class LinkInfo()
{
    public abstract void Encode(BinaryWriter driver);

    public abstract ushort GetEncodeSize();
}

internal record class HardLinkInfo(
    ulong HeaderAddress
) : LinkInfo
{
    public static HardLinkInfo Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new HardLinkInfo(
            HeaderAddress: superblock.ReadOffset(driver)
        );
    }

    public override void Encode(BinaryWriter driver)
    {
        driver.Write(HeaderAddress);
    }

    public override ushort GetEncodeSize()
    {
        return sizeof(ulong);
    }
}

internal record class SoftLinkInfo(
    string Value
) : LinkInfo
{
    public static SoftLinkInfo Decode(H5DriverBase driver)
    {
        var valueLength = driver.ReadUInt16();
        var value = ReadUtils.ReadFixedLengthString(driver, valueLength);

        return new SoftLinkInfo(
            Value: value
        );
    }

    public override void Encode(BinaryWriter driver)
    {
        throw new NotImplementedException();
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }
}

internal record class ExternalLinkInfo(
    string FilePath,
    string FullObjectPath
) : LinkInfo
{
    private byte _version;

    private byte _flags;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ExternalLinkInfo)} are supported.");

            _version = value;
        }
    }

    public required byte Flags
    {
        get
        {
            return _flags;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"The flags of an {nameof(FillValueMessage)} instance must be equal to zero.");

            _flags = value;
        }
    }

    public static ExternalLinkInfo Decode(H5DriverBase driver)
    {
        // value length
        var _ = driver.ReadUInt16();

        // version and flags
        var data = driver.ReadByte();
        var version = (byte)((data & 0xF0) >> 4); // take only upper 4 bits
        var flags = (byte)((data & 0x0F) >> 0); // take only lower 4 bits

        // file name
        var filePath = ReadUtils.ReadNullTerminatedString(driver, pad: false);

        // full object path
        var fullObjectPath = ReadUtils.ReadNullTerminatedString(driver, pad: false);

        return new ExternalLinkInfo(
            FilePath: filePath,
            FullObjectPath: fullObjectPath
        )
        {
            Version = version,
            Flags = flags
        };
    }

    public override void Encode(BinaryWriter driver)
    {
        throw new NotImplementedException();
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }
}

internal record class UserDefinedLinkInfo(
    byte[] Data
) : LinkInfo
{
    public static UserDefinedLinkInfo Decode(H5DriverBase driver)
    {
        var dataLength = driver.ReadUInt16();

        return new UserDefinedLinkInfo(
            Data: driver.ReadBytes(dataLength)
        );
    }

    public override void Encode(BinaryWriter driver)
    {
        throw new NotImplementedException();
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }
}