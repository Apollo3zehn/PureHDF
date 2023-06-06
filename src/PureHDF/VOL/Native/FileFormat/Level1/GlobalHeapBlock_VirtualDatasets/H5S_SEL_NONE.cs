namespace PureHDF.VOL.Native;

internal record class H5S_SEL_NONE(
    //
) : H5S_SEL
{
    private uint _version;

    public required uint Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_NONE)} are supported.");

            _version = value;
        }
    }

    public static H5S_SEL_NONE Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadUInt32();

        // reserved
        driver.ReadBytes(8);

        return new H5S_SEL_NONE(
            //
        )
        {
            Version = version
        };
    }

    public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
    {
        return default;
    }

    public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
    {
        throw new Exception("This should never happen.");
    }
}