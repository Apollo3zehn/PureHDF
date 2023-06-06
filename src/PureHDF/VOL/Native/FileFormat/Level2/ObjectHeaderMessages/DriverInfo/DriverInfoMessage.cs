namespace PureHDF.VOL.Native;

internal record class DriverInfoMessage(
    string DriverId,
    ushort DriverInfoSize,
    DriverInfo DriverInfo
) : Message
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
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoMessage)} are supported.");

            _version = value;
        }
    }

    public static DriverInfoMessage Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadByte();

        // driver id
        var driverId = ReadUtils.ReadFixedLengthString(driver, 8);

        // driver info size
        var driverInfoSize = driver.ReadUInt16();

        // driver info
        DriverInfo driverInfo = driverId switch
        {
            "NCSAmulti" => MultiDriverInfo.Decode(driver),
            "NCSAfami" => FamilyDriverInfo.Decode(driver),
            _ => throw new NotSupportedException($"The driver ID '{driverId}' is not supported.")
        };

        return new DriverInfoMessage(
            DriverId: driverId,
            DriverInfoSize: driverInfoSize,
            DriverInfo: driverInfo
        )
        {
            Version = version
        };
    }
}