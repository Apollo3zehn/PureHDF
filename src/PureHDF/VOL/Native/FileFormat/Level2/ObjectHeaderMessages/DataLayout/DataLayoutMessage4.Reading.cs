namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4(
    LayoutClass LayoutClass,
    StoragePropertyDescription Properties
) : DataLayoutMessage3(LayoutClass, Properties)
{
    internal static new async ValueTask<DataLayoutMessage4> Decode(NativeReadContext context, byte version)
    {
        var (driver, _) = context;

        // layout class
        var layoutClass = (LayoutClass)await driver.ReadByte().ConfigureAwait(false);

        // storage property description
        StoragePropertyDescription properties = (version, layoutClass) switch
        {
            (_, LayoutClass.Compact) => await CompactStoragePropertyDescription.Decode(driver).ConfigureAwait(false),
            (_, LayoutClass.Contiguous) => await ContiguousStoragePropertyDescription.Decode(context).ConfigureAwait(false),
            (4, LayoutClass.Chunked) => await ChunkedStoragePropertyDescription4.Decode(context).ConfigureAwait(false),
            (4, LayoutClass.VirtualStorage) => await VirtualStoragePropertyDescription.Decode(context).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported for the data layout message version '{version}'.")
        };

        // address
        var address = properties.Address;

        return new DataLayoutMessage4(
            LayoutClass: layoutClass,
            Properties: properties
        )
        {
            Version = version
        };
    }
}