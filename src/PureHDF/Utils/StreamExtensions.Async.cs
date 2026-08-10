namespace PureHDF;

internal static class StreamAsyncExtensions
{
    /// <summary>
    ///     Async exact-read that works on every target framework (Stream.ReadExactlyAsync is
    ///     .NET 7+). Mirrors the sync ReadExactly polyfill in StreamExtensions.cs.
    /// </summary>
    public static async ValueTask ReadExactlyAsyncCompat(this Stream stream, Memory<byte> buffer)
    {
        var remaining = buffer;

        while (remaining.Length > 0)
        {
            var read = await stream.ReadAsync(remaining).ConfigureAwait(false);

            if (read == 0)
                throw new EndOfStreamException();

            remaining = remaining[read..];
        }
    }
}
