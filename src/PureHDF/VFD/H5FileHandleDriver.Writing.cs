#if NET6_0_OR_GREATER

namespace PureHDF.VFD;

internal partial class H5FileHandleDriver : H5DriverBase
{
    public override void Write(Span<byte> data)
    {
        throw new NotImplementedException();
    }

    public override void WriteDataset(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long endAddress)
    {
        throw new NotImplementedException();
    }
}

#endif