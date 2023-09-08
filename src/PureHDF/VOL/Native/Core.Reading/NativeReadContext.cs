namespace PureHDF.VOL.Native;

internal record class NativeReadContext(
    H5DriverBase Driver,
    Superblock Superblock
)
{
    public required H5ReadOptions ReadOptions { get; init; }
    
    public NativeFile File { get; set; } = default!;
};