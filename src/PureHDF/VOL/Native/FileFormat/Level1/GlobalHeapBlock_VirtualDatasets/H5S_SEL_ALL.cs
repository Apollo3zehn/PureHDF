namespace PureHDF.VOL.Native;

internal record class H5S_SEL_ALL(

) : H5S_SEL
{
    private uint _version;

    public static H5S_SEL_ALL Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadUInt32();

        // reserved
        driver.ReadBytes(8);

        return new H5S_SEL_ALL(
        //
        )
        {
            Version = version
        };
    }

    public required uint Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_ALL)} are supported.");

            _version = value;
        }
    }

    public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
    {
        var linearIndex = MathUtils.ToLinearIndex(coordinates, sourceDimensions);
        var maxCount = sourceDimensions[^1] - coordinates[^1];

        return new LinearIndexResult(
            Success: true, // TODO theoretically this can fail
            linearIndex,
            maxCount);
    }

    public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
    {
        var coordinates = MathUtils.ToCoordinates(linearIndex, sourceDimensions);
        var maxCount = sourceDimensions[^1] - coordinates[^1];

        // TODO theoretically this can fail
        return new CoordinatesResult(
            coordinates,
            maxCount);
    }
}