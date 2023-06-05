namespace PureHDF.VOL.Native;

internal class OpaquePropertyDescription : DatatypePropertyDescription
{
    #region Constructors

    public OpaquePropertyDescription(H5DriverBase driver, byte tagByteLength)
    {
        Tag = ReadUtils
            .ReadFixedLengthString(driver, tagByteLength)
            .TrimEnd('\0');

        // TODO: How to avoid the appended '\0'? Is this caused by C# string passed to HDF5 lib?
    }

    #endregion

    #region Properties

    public string Tag { get; set; }

    #endregion
}