namespace PureHDF;

/// <summary>
/// Contains a collection of optional methods to improve performance.
/// </summary>
public interface IDatasetStream
{
    /// <summary>
    /// This method is called to indicate that actual data is being read (e.g. to determine if the request should be cached).
    /// </summary>
    /// <param name="buffer">The buffer to write the data into.</param>
    public int ReadDataset(Memory<byte> buffer);

    /// <summary>
    /// This method is called to indicate that actual data is being read (e.g. to determine if the request should be cached).
    /// </summary>
    /// <param name="buffer">The buffer to write the data into.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    public ValueTask<int> ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}