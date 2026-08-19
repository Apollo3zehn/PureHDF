namespace PureHDF.VOL.Native;

internal record class DataLayoutMessage3(
    LayoutClass LayoutClass,
    StoragePropertyDescription Properties
) : DataLayoutMessage(LayoutClass)
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

    internal static async ValueTask<DataLayoutMessage3> Decode(NativeReadContext context, byte version)
    {
        var (driver, _) = context;

        // layout class
        var layoutClass = (LayoutClass)await driver.ReadByte().ConfigureAwait(false);

        // storage property description
        StoragePropertyDescription properties = (version, layoutClass) switch
        {
            (_, LayoutClass.Compact) => await CompactStoragePropertyDescription.Decode(driver).ConfigureAwait(false),
            (_, LayoutClass.Contiguous) => await ContiguousStoragePropertyDescription.Decode(context).ConfigureAwait(false),
            (3, LayoutClass.Chunked) => await ChunkedStoragePropertyDescription3.Decode(context).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported for the data layout message version '{version}'.")
        };

        // address
        return new DataLayoutMessage3(
            LayoutClass: layoutClass,
            Properties: properties
        )
        {
            Version = version
        };
    }
}