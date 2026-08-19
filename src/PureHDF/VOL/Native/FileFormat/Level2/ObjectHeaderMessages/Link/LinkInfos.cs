using System.Text;

namespace PureHDF.VOL.Native;

internal abstract record class LinkInfo()
{
    public abstract ushort GetEncodeSize();

    public abstract void Encode(H5DriverBase driver);
}

internal record class HardLinkInfo(
    ulong HeaderAddress
) : LinkInfo
{
    public static async ValueTask<HardLinkInfo> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new HardLinkInfo(
            HeaderAddress: await superblock.ReadOffset(driver).ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize()
    {
        return sizeof(ulong);
    }

    public override void Encode(H5DriverBase driver)
    {
        driver.Write(HeaderAddress);
    }
}

internal record class SoftLinkInfo(
    string Value
) : LinkInfo
{
    public static async ValueTask<SoftLinkInfo> Decode(H5DriverBase driver)
    {
        var valueLength = await driver.ReadUInt16().ConfigureAwait(false);

        // The value has no character set field of its own and is a path made of link names,
        // which may be UTF-8; decoded as ASCII it no longer names the object it points at
        // and the link silently fails to resolve. Encode below writes UTF-8.
        var value = await ReadUtils.ReadFixedLengthString(driver, valueLength).ConfigureAwait(false);

        return new SoftLinkInfo(
            Value: value
        );
    }

    public override ushort GetEncodeSize()
    {
        var nameBytes = Encoding.UTF8.GetBytes(Value);

        return (ushort)(
            sizeof(ushort) + 
            (ushort)nameBytes.Length
        );
    }

    public override void Encode(H5DriverBase driver)
    {
        var nameBytes = Encoding.UTF8.GetBytes(Value);

        driver.Write((ushort)nameBytes.Length);
        driver.Write(nameBytes);
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

    public static async ValueTask<ExternalLinkInfo> Decode(H5DriverBase driver)
    {
        // value length
        var _ = await driver.ReadUInt16().ConfigureAwait(false);

        // version and flags
        var data = await driver.ReadByte().ConfigureAwait(false);
        var version = (byte)((data & 0xF0) >> 4); // take only upper 4 bits
        var flags = (byte)((data & 0x0F) >> 0); // take only lower 4 bits

        // file name
        var filePath = await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false);

        // full object path
        var fullObjectPath = await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false);

        return new ExternalLinkInfo(
            FilePath: filePath,
            FullObjectPath: fullObjectPath
        )
        {
            Version = version,
            Flags = flags
        };
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}

internal record class UserDefinedLinkInfo(
    byte[] Data
) : LinkInfo
{
    public static async ValueTask<UserDefinedLinkInfo> Decode(H5DriverBase driver)
    {
        var dataLength = await driver.ReadUInt16().ConfigureAwait(false);

        return new UserDefinedLinkInfo(
            Data: await driver.ReadBytes(dataLength).ConfigureAwait(false)
        );
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}