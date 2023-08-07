namespace PureHDF;

internal partial class OffsetStream : IH5ReadStream, IH5WriteStream
{
    public void WriteDataset(Span<byte> buffer)
    {
        _driver.WriteDataset(buffer);
    }
}