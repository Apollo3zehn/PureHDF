namespace PureHDF.VOL.Native;

/// <summary>
/// Caches chunks during read operations.
/// </summary>
public interface IReadingChunkCache
{
    /// <summary>
    /// Tries to get the chunk at the given index.
    /// </summary>
    /// <param name="chunkIndex">The linear chunk index.</param>
    /// <param name="chunkReader">The chunk reader is used whenever the chunk is not already cached.</param>
    public Memory<byte> GetChunk(
        ulong chunkIndex,
        Func<Memory<byte>> chunkReader);

    /// <summary>
    /// Tries to get the chunk at the given index without blocking on the chunk reader.
    /// </summary>
    /// <param name="chunkIndex">The linear chunk index.</param>
    /// <param name="chunkReader">The chunk reader is used whenever the chunk is not already cached.</param>
    /// <remarks>
    /// Used by the asynchronous read path. Reading a chunk is where a chunked dataset does its actual
    /// I/O, so a cache that can only be filled synchronously would make <c>ReadAsync</c> block on
    /// every cache miss - which is most of a first read.
    /// <para>
    /// The default implementation bridges to <see cref="GetChunk" />, so an existing cache keeps
    /// compiling and working unchanged; it simply blocks, exactly as it did before. Override this to
    /// participate in a genuinely asynchronous read (see <c>SimpleReadingChunkCache</c>, where both
    /// methods share their cache bookkeeping and differ only in how the reader is invoked).
    /// </para>
    /// </remarks>
    public ValueTask<Memory<byte>> GetChunkAsync(
        ulong chunkIndex,
        Func<ValueTask<Memory<byte>>> chunkReader)
    {
        return new ValueTask<Memory<byte>>(
            GetChunk(chunkIndex, () => chunkReader().GetAwaiter().GetResult()));
    }
}