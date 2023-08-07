namespace PureHDF.VFD;

internal partial class H5StreamDriver : H5DriverBase
{
    public override void Write(Span<byte> data)
    {
        _stream.Write(data);
    }

    public override void WriteDataset(Span<byte> buffer)
    {
        _stream.Write(buffer);
    }

    public override void SetLength(long endAddress)
    {
        _stream.SetLength(endAddress);
    }
}