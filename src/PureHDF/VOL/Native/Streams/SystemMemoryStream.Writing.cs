namespace PureHDF;

internal partial class SystemMemoryStream : IH5WriteStream
{
    public void WriteDataset(Span<byte> buffer)
    {
        var length = Math.Min(SlicedMemory.Length, buffer.Length);

        buffer[..length]
            .CopyTo(SlicedMemory.Span);

        Seek(length, SeekOrigin.Current);
    }
}