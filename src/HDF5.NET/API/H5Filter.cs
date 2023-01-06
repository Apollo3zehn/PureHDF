namespace HDF5.NET
{
    /// <summary>
    /// A delegate which describes a filter function.
    /// </summary>
    /// <param name="flags">The filter flags.</param>
    /// <param name="parameters">The filter parameters.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>The target buffer.</returns>
    public delegate Memory<byte> FilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer);

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
        /// <param name="filterFunc">The filter function.</param>
        public static void Register(H5FilterID identifier, string name, FilterFunc filterFunc)
        {
            var registration = new H5FilterRegistration((FilterIdentifier)identifier, name, filterFunc);
            H5Filter.Registrations.AddOrUpdate((FilterIdentifier)identifier, registration, (_, oldRegistration) => registration);
        }
    }
}
