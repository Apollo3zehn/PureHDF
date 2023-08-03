namespace PureHDF;

internal partial class OffsetStream : IH5ReadStream, IH5WriteStream
{
    public void Write(Span<byte> buffer)
    {
        _driver.Write(buffer);
    }
}