namespace PureHDF;

internal record class NativeContext(
    H5DriverBase Driver,
    Superblock Superblock
)
{
    public NativeFile File { get; set; } = default!;
};