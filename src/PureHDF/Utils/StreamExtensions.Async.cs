namespace PureHDF;

internal static class StreamAsyncExtensions
{
    /// <summary>
    ///     Async exact-read that works on every target framework (Stream.ReadExactlyAsync is
    ///     .NET 7+). Mirrors the sync ReadExactly polyfill in StreamExtensions.cs.
    /// </summary>
    /// <remarks>
    ///     Deliberately not an <c>async</c> method. Most streams under the read path satisfy a read
    ///     without ever suspending - a MemoryStream always does, a buffered FileStream does whenever
    ///     the bytes are already in its buffer - and an unconditional <c>async</c> method would still
    ///     build a state machine for each of them. Draining the synchronous case in a plain loop and
    ///     deferring to <see cref="ReadExactlyAsyncSlow" /> only on a genuine suspension keeps the
    ///     common case allocation-free while remaining fully async for a remote stream.
    /// </remarks>
    public static ValueTask ReadExactlyAsyncCompat(this Stream stream, Memory<byte> buffer)
    {
        var remaining = buffer;

        while (remaining.Length > 0)
        {
            var pending = stream.ReadAsync(remaining);

            if (!pending.IsCompletedSuccessfully)
                return ReadExactlyAsyncSlow(stream, remaining, pending);

            var read = pending.Result;

            if (read == 0)
                throw new EndOfStreamException();

            remaining = remaining[read..];
        }

        return default;
    }

    private static async ValueTask ReadExactlyAsyncSlow(
        Stream stream,
        Memory<byte> remaining,
        ValueTask<int> pending)
    {
        while (true)
        {
            var read = await pending.ConfigureAwait(false);

            if (read == 0)
                throw new EndOfStreamException();

            remaining = remaining[read..];

            if (remaining.Length == 0)
                return;

            pending = stream.ReadAsync(remaining);
        }
    }
}
