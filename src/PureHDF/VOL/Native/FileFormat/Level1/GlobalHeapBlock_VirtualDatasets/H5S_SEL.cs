namespace PureHDF.VOL.Native;

internal abstract record class H5S_SEL(
    //
)
{
    public abstract LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates);

    public abstract CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex);

    public static ulong ReadEncodedValue(H5DriverBase driver, byte encodeSize)
    {
        return encodeSize switch
        {
            2 => driver.ReadUInt16(),
            4 => driver.ReadUInt32(),
            8 => driver.ReadUInt64(),
            _ => throw new Exception($"Invalid encode size {encodeSize}.")
        };
    }
}