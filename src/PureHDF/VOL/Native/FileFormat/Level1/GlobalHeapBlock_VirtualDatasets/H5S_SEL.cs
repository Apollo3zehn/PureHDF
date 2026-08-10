namespace PureHDF.VOL.Native;

internal abstract record class H5S_SEL(
//
)
{
    public abstract LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates);

    public abstract CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex);

    public static async ValueTask<ulong> ReadEncodedValue(H5DriverBase driver, byte encodeSize)
    {
        return encodeSize switch
        {
            2 => await driver.ReadUInt16().ConfigureAwait(false),
            4 => await driver.ReadUInt32().ConfigureAwait(false),
            8 => await driver.ReadUInt64().ConfigureAwait(false),
            _ => throw new Exception($"Invalid encode size {encodeSize}.")
        };
    }
}