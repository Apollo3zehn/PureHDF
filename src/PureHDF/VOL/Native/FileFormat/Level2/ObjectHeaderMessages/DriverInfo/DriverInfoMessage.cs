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

    public static async ValueTask<DriverInfoMessage> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // driver id
        var driverId = await ReadUtils.ReadFixedLengthString(driver, 8).ConfigureAwait(false);

        // driver info size
        var driverInfoSize = await driver.ReadUInt16().ConfigureAwait(false);

        // driver info
        DriverInfo driverInfo = driverId switch
        {
            "NCSAmulti" => await MultiDriverInfo.Decode(driver).ConfigureAwait(false),
            "NCSAfami" => await FamilyDriverInfo.Decode(driver).ConfigureAwait(false),
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