namespace PureHDF.VOL.Native;

internal readonly record struct DataspaceSelection(
    SelectionType Type,
    H5S_SEL Info
)
{
    public static DataspaceSelection Decode(H5DriverBase driver)
    {
        var type = (SelectionType)driver.ReadUInt32();

        var info = type switch
        {
            SelectionType.H5S_SEL_NONE => H5S_SEL_NONE.Decode(driver),
            SelectionType.H5S_SEL_POINTS => H5S_SEL_POINTS.Decode(driver),
            SelectionType.H5S_SEL_HYPER => H5S_SEL_HYPER.Decode(driver),
            SelectionType.H5S_SEL_ALL => H5S_SEL_ALL.Decode(driver),
            SelectionType.H5S_SEL_POINTS_SPECIAL_HANDLING => SpecialHandling(driver),
            _ => throw new NotSupportedException($"The dataspace selection type '{type}' is not supported.")
        };

        return new DataspaceSelection(
            Type: type,
            Info: info
        );
    }

    private static H5S_SEL SpecialHandling(H5DriverBase driver)
    {
        // jump position
        var jumpPosition = driver.ReadUInt32();
        var points = H5S_SEL_POINTS.Decode(driver);

        driver.Seek(jumpPosition, SeekOrigin.Begin);

        return points;
    }
}