namespace PureHDF.VOL.Native;

internal record class NativeReadContext(
    H5DriverBase Driver,
    Superblock Superblock
)
{
    public NativeFile File { get; set; } = default!;
};