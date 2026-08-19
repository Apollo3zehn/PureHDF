namespace PureHDF;

/// <summary>
/// Implemented by a class that can serve PureHDF's reads by absolute offset instead of through a
/// stream cursor, and that is safe to read from concurrently.
/// </summary>
/// <remarks>
///     Implementing this interface has two effects.
///     <para>
///         First, it makes the source concurrency-capable. PureHDF isolates a cursor per read
///         operation, so a dataset or attribute resolved once can be read from several threads
///         through a single <c>H5File</c>. A cursor-based stream cannot participate in that - a
///         second reader would share the one cursor - whereas every read declared here carries the
///         offset it wants, so no cursor is involved at all.
///     </para>
///     <para>
///         Second, it tells the implementation which reads are worth caching. A range-request
///         source typically wants to cache small structural reads aggressively - the same superblock,
///         object header and B-tree bytes are re-read constantly - while streaming bulk payload
///         straight through, since caching a multi-megabyte chunk that is decoded once only wastes
///         memory. That is the whole reason there are two methods rather than one; both do the same
///         thing to the buffer.
///     </para>
///     <para>
///         Both methods are required. Neither has a default implementation, deliberately: a source
///         that silently fell back to a cursor-based read would reintroduce exactly the sharing
///         problem this interface exists to remove.
///     </para>
///     <para>
///         THREAD SAFETY: both methods must be safe to call concurrently on the same instance,
///         including with overlapping ranges. That is the point of the interface; PureHDF will issue
///         concurrent calls whenever the caller reads concurrently.
///     </para>
///     <para>
///         An implementation that also inherits from <see cref="Stream" /> may still be passed to the
///         <see cref="H5File.Open(Stream, bool, H5ReadOptions?)" /> overloads, but it is NOT
///         automatically promoted to positionless mode there: it runs in cursor mode like any other
///         <see cref="Stream" />, and the concurrency this interface offers is not used. Pass it to
///         the <see cref="H5File.Open(IConcurrentStream, bool, H5ReadOptions?)" /> overload to drive
///         it positionlessly.
///     </para>
/// </remarks>
public interface IConcurrentStream : IDisposable
{
    /// <summary>
    /// The total length, in bytes, of the data this source exposes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Reads bulk dataset payload: the actual data of a dataset or attribute.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset, in bytes, from the start of the source's data - not relative to any
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
    public ValueTask ReadDatasetAsync(long offset, Memory<byte> buffer);

    /// <summary>
    /// Reads file structure: superblock, object headers, B-tree and heap nodes, chunk index records
    /// and similar.
    /// </summary>
    /// <param name="offset">
    /// The absolute offset, in bytes, from the start of the source's data - not relative to any
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
    public ValueTask ReadMetadataAsync(long offset, Memory<byte> buffer);
}
