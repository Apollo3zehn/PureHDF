namespace PureHDF;

/// <summary>
/// Implemented by a <see cref="Stream" /> that can serve PureHDF's reads by absolute offset instead
/// of through the stream cursor.
/// </summary>
/// <remarks>
///     Implementing this interface has two effects.
///     <para>
///         First, it makes the stream concurrency-capable. PureHDF isolates a cursor per read
///         operation, so a dataset or attribute resolved once can be read from several threads
///         through a single <c>H5File</c>. A cursor-based stream cannot participate in that - a
///         second reader would share the one cursor - whereas every read declared here carries the
///         offset it wants, so no cursor is involved at all.
///     </para>
///     <para>
///         Second, it tells the implementation which reads are worth caching. A range-request
///         stream typically wants to cache small structural reads aggressively - the same superblock,
///         object header and B-tree bytes are re-read constantly - while streaming bulk payload
///         straight through, since caching a multi-megabyte chunk that is decoded once only wastes
///         memory. That is the whole reason there are two methods rather than one; both do the same
///         thing to the buffer.
///     </para>
///     <para>
///         Both methods are required. Neither has a default implementation, deliberately: a stream
///         that silently fell back to a cursor-based read would reintroduce exactly the sharing
///         problem this interface exists to remove.
///     </para>
///     <para>
///         THREAD SAFETY: both methods must be safe to call concurrently on the same instance,
///         including with overlapping ranges. That is the point of the interface; PureHDF will issue
///         concurrent calls whenever the caller reads concurrently. Note that the inherited
///         <see cref="Stream" /> members (<see cref="Stream.Read(byte[], int, int)" />,
///         <see cref="Stream.Seek" />, <see cref="Stream.Position" />) are a separate, cursor-based
///         contract and carry no such requirement - PureHDF does not use them once this interface is
///         implemented.
///     </para>
/// </remarks>
public interface IDatasetStream
{
    /// <summary>
    /// Reads bulk dataset payload: the actual data of a dataset or attribute.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset, in bytes, from the start of the stream's data - not relative to any
    /// cursor. Implementations must neither depend on nor mutate a cursor.
    /// </param>
    /// <param name="buffer">
    /// The buffer to write the data into. It must be filled completely; a short read is an error and
    /// should throw (for example <see cref="EndOfStreamException" />).
    /// </param>
    /// <remarks>
    /// This is the "actual data" signal: an implementation that caches may want to bypass its cache
    /// here, because bulk payload is usually large and read once. Must be safe to call concurrently.
    /// </remarks>
    public ValueTask ReadDataset(long offset, Memory<byte> buffer);

    /// <summary>
    /// Reads file structure: superblock, object headers, B-tree and heap nodes, chunk index records
    /// and similar.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset, in bytes, from the start of the stream's data - not relative to any
    /// cursor. Implementations must neither depend on nor mutate a cursor.
    /// </param>
    /// <param name="buffer">
    /// The buffer to write the data into. It must be filled completely; a short read is an error and
    /// should throw (for example <see cref="EndOfStreamException" />).
    /// </param>
    /// <remarks>
    /// These reads are small - often two or eight bytes - numerous, and highly repetitive, so an
    /// implementation over a remote source will usually want to serve them from a cache of larger
    /// blocks. Must be safe to call concurrently.
    /// </remarks>
    public ValueTask ReadMetadata(long offset, Memory<byte> buffer);
}
