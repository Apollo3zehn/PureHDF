namespace PureHDF.VOL.Native;

internal record NativeWriteContext(
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Dataset, (H5D_Base H5D, object Encode)> DatasetToInfoMap,
    Dictionary<DatasetInfo, (long ObjectHeaderStart, int ObjectHeaderLength)> DatasetInfoToObjectHeaderMap,
    Dictionary<Type, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<H5Object, ulong> ObjectToAddressMap,
    SystemMemoryStream ShortlivedStream
);