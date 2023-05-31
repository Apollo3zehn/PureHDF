namespace PureHDF.VOL.Native;

internal class DriverInfoMessage : Message
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public DriverInfoMessage(H5DriverBase driver)
    {
        // version
        Version = driver.ReadByte();

        // driver id
        DriverId = ReadUtils.ReadFixedLengthString(driver, 8);

        // driver info size
        DriverInfoSize = driver.ReadUInt16();

        // driver info
        DriverInfo = DriverId switch
        {
            "NCSAmulti" => MultiDriverInfo.Decode(driver),
            "NCSAfami" => FamilyDriverInfo.Decode(driver),
            _ => throw new NotSupportedException($"The driver ID '{DriverId}' is not supported.")
        };
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoMessage)} are supported.");

            _version = value;
        }
    }

    public string DriverId { get; set; }
    public ushort DriverInfoSize { get; set; }
    public DriverInfo DriverInfo { get; set; }

    #endregion
}