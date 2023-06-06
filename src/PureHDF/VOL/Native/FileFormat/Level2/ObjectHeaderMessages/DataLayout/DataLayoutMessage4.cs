namespace PureHDF.VOL.Native;

internal record class DataLayoutMessage4(
    LayoutClass LayoutClass,
    ulong Address,
    StoragePropertyDescription Properties
) : DataLayoutMessage3(LayoutClass, Address, Properties);