namespace PureHDF;

/// <summary>
/// Represents an HDF5 filter.
/// </summary>
public interface IH5Filter
{
    /// <summary>
    /// The filter identifier.
    /// </summary>
    H5FilterID Id { get; }

    /// <summary>
    /// The filter name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The filter function.
    /// </summary>
    /// <param name="info">Additional information for the filter process.</param>
    /// <returns>The filtered data.</returns>
    Memory<byte> Filter(FilterInfo info);

    /// <summary>
    /// Returns the filter parameters being stored in the HDF5 file and which will be provided to the filter function. This method is only required for the PureHDF write API.
    /// </summary>
    /// <param name="dataset">The dataset for which the parameters should be returned.</param>
    /// <param name="options">The user defined map of filter options.</param>
    /// <returns>The filter parameters.</returns>
    uint[] GetParameters(H5Dataset dataset, Dictionary<string, object> options); /* https://docs.hdfgroup.org/hdf5/develop/group___h5_z.html#ga93145acc38c2c60d832b7a9b0123706b */
}