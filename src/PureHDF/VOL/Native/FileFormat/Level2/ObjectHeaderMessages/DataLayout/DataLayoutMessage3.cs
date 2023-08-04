namespace PureHDF.VOL.Native;

internal record class DataLayoutMessage3(
    LayoutClass LayoutClass,
    ulong Address,
    StoragePropertyDescription Properties
) : DataLayoutMessage(LayoutClass, Address)
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
            if (!(3 <= value && value <= 4))
                throw new FormatException($"Only version 3 and version 4 instances of type {nameof(DataLayoutMessage3)} are supported.");

            _version = value;
        }
    }

    internal static DataLayoutMessage3 Decode(NativeReadContext context, byte version)
    {
        var (driver, _) = context;

        // layout class
        var layoutClass = (LayoutClass)driver.ReadByte();

        // storage property description
        StoragePropertyDescription properties = (version, layoutClass) switch
        {
            (_, LayoutClass.Compact) => CompactStoragePropertyDescription.Decode(driver),
            (_, LayoutClass.Contiguous) => ContiguousStoragePropertyDescription.Decode(context),
            (3, LayoutClass.Chunked) => ChunkedStoragePropertyDescription3.Decode(context),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported for the data layout message version '{version}'.")
        };

        // address
        var address = properties.Address;

        return new DataLayoutMessage3(
            LayoutClass: layoutClass,
            Address: address,
            Properties: properties
        )
        {
            Version = version
        };
    }
}