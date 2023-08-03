namespace PureHDF.VFD;

internal unsafe partial class H5MemoryMappedFileDriver : H5DriverBase
{
    public override void Write(Span<byte> data)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long endAddress)
    {
        throw new NotImplementedException();
    }
}