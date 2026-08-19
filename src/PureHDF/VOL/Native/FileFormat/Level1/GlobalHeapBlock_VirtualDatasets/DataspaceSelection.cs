namespace PureHDF.VOL.Native;

internal readonly record struct DataspaceSelection(
    SelectionType Type,
    H5S_SEL Info
)
{
    public static async ValueTask<DataspaceSelection> Decode(H5DriverBase driver)
    {
        var type = (SelectionType)(await driver.ReadUInt32().ConfigureAwait(false));

        var info = type switch
        {
            SelectionType.H5S_SEL_NONE => await H5S_SEL_NONE.Decode(driver).ConfigureAwait(false),
            SelectionType.H5S_SEL_POINTS => await H5S_SEL_POINTS.Decode(driver).ConfigureAwait(false),
            SelectionType.H5S_SEL_HYPER => await H5S_SEL_HYPER.Decode(driver).ConfigureAwait(false),
            SelectionType.H5S_SEL_ALL => await H5S_SEL_ALL.Decode(driver).ConfigureAwait(false),
            SelectionType.H5S_SEL_POINTS_SPECIAL_HANDLING => await SpecialHandling(driver).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The dataspace selection type '{type}' is not supported.")
        };

        return new DataspaceSelection(
            Type: type,
            Info: info
        );
    }

    private static async ValueTask<H5S_SEL> SpecialHandling(H5DriverBase driver)
    {
        // jump position
        var jumpPosition = await driver.ReadUInt32().ConfigureAwait(false);
        var points = await H5S_SEL_POINTS.Decode(driver).ConfigureAwait(false);

        driver.SeekRelativeToBaseAddress(jumpPosition);

        return points;
    }
}