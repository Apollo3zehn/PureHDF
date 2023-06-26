namespace PureHDF.Filters;

/// <summary>
/// A delegate which describes a filter function.
/// </summary>
/// <param name="Flags">The filter flags.</param>
/// <param name="Parameters">The filter parameters.</param>
/// <param name="IsLast">A boolean which indicates if this is the last filter in the pipeline.</param>
/// <param name="ChunkSize">The chunk size.</param>
/// <param name="SourceBuffer">The source buffer.</param>
/// <param name="GetBuffer">A function to get the target buffer. The buffer is valid only for the lifetime of this method.</param>
/// <returns>The target buffer.</returns>
public record class FilterInfo(
    H5FilterFlags Flags,
    uint[] Parameters,
    bool IsLast,
    int ChunkSize,
    Memory<byte> SourceBuffer,
    Func<int, Memory<byte>> GetBuffer);

/// <summary>
/// A class to manage filters.
/// </summary>
public static partial class H5Filter
{
    /// <summary>
    /// Registers a new filter.
    /// </summary>
    /// <param name="identifier">The filter identifier.</param>
    /// <param name="name">The filter name.</param>
    /// <param name="filterFunction">The filter function.</param>
    public static void Register(
        H5FilterID identifier,
         string name, 
         Func<FilterInfo, Memory<byte>> filterFunction)
    {
        var registration = new H5FilterRegistration(
            (FilterIdentifier)identifier, 
            name, 
            filterFunction);

        Registrations
            .AddOrUpdate((FilterIdentifier)identifier, registration, (_, oldRegistration) => registration);
    }
}