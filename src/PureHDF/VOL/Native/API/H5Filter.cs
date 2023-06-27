namespace PureHDF.Filters;

/// <summary>
/// A delegate which describes a filter function.
/// </summary>
/// <param name="Flags">The filter flags.</param>
/// <param name="Parameters">The filter parameters.</param>
/// <param name="ChunkSize">The chunk size.</param>
/// <param name="SourceBuffer">The source buffer.</param>
/// <param name="FinalBuffer">The final buffer. The final buffer is non-default if the current filter is the last one in the pipeline.</param>
/// <returns>The target buffer.</returns>
public record class FilterInfo(
    H5FilterFlags Flags,
    uint[] Parameters,
    int ChunkSize,
    Memory<byte> SourceBuffer,
    Memory<byte> FinalBuffer);

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