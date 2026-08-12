namespace PureHDF;

#if !NET8_0_OR_GREATER

internal static partial class StreamExtensions
{
    public static void ReadExactly(this Stream stream, Span<byte> buffer)
    {
        var slicedBuffer = buffer;

        while (slicedBuffer.Length > 0)
        {
            var readBytes = stream.Read(slicedBuffer);

            // Read returns 0 only at end of stream, so without this a truncated file spins here
            // forever instead of failing. Matches Stream.ReadExactly on net8.0+, which is what this
            // shim stands in for, and ReadExactlyAsyncCompat on the async path.
            if (readBytes == 0)
                throw new EndOfStreamException();

            slicedBuffer = slicedBuffer[readBytes..];
        };
    }
}

#endif