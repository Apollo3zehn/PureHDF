namespace PureHDF.VOL.Native;

internal record NativeWriteContext(
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Dataset, H5D_Base> DatasetToH5DMap,
    Dictionary<Type, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<object, ulong> ObjectToAddressMap,
    SystemMemoryStream ShortlivedStream
);