namespace PureHDF.VOL.Native;

internal class H5S_SEL_NONE : H5S_SEL
{
    #region Fields

    private uint _version;

    #endregion

    #region Constructors

    public H5S_SEL_NONE(H5DriverBase driver)
    {
        // version
        Version = driver.ReadUInt32();

        // reserved
        driver.ReadBytes(8);
    }

    #endregion

    #region Properties

    public uint Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_NONE)} are supported.");

            _version = value;
        }
    }

    public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
    {
        return default;
    }

    public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
    {
        throw new Exception("This should never happen.");
    }

    #endregion
}