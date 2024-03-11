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
    public void ReadDataset(Span<byte> buffer);
}