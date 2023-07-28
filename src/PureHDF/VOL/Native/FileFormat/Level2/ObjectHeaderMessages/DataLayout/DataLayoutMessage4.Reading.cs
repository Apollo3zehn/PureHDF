namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4(
    LayoutClass LayoutClass,
    ulong Address,
    StoragePropertyDescription Properties
) : DataLayoutMessage3(LayoutClass, Address, Properties)
{
    internal static new DataLayoutMessage4 Decode(NativeContext context, byte version)
    {
        var (driver, _) = context;

        // layout class
        var layoutClass = (LayoutClass)driver.ReadByte();

        // storage property description
        StoragePropertyDescription properties = (version, layoutClass) switch
        {
            (_, LayoutClass.Compact) => CompactStoragePropertyDescription.Decode(driver),
            (_, LayoutClass.Contiguous) => ContiguousStoragePropertyDescription.Decode(context),
            (4, LayoutClass.Chunked) => ChunkedStoragePropertyDescription4.Decode(context),
            (4, LayoutClass.VirtualStorage) => VirtualStoragePropertyDescription.Decode(context),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported for the data layout message version '{version}'.")
        };

        // address
        var address = properties.Address;

        return new DataLayoutMessage4(
            LayoutClass: layoutClass,
            Address: address,
            Properties: properties
        )
        {
            Version = version
        };
    }
}