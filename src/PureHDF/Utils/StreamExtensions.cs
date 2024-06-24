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
            slicedBuffer = slicedBuffer[readBytes..];
        };
    }
}

#endif