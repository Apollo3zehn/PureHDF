namespace PureHDF;

internal readonly record struct NativeContext(
    H5DriverBase Driver,
    Superblock Superblock
);