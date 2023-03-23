namespace PureHDF;

/// <summary>
/// Defines extensions methods for the <see cref="IH5Dataset" /> type.
/// </summary>
public static class IH5DatasetExtensions
{
    /// <summary>
    /// Queries the data. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#43-experimental-iqueryable-1-dimensional-data-only">PureHDF</seealso>.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="dataset">The dataset to operate on.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>A queryable of type <typeparamref name="T"/>.</returns>
    public static IQueryable<T> AsQueryable<T>(this IH5Dataset dataset, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged
    {
        if (dataset.Space.Rank != 1)
            throw new Exception("Querying data only works for 1-dimensional datasets.");

        var provider = new QueryProvider<T>(
            datasetLength: dataset.Space.Dimensions[0],
            executor: fileSelection => dataset.Read<T>(fileSelection, memorySelection, memoryDims));

        var queryable = new Queryable<T>(provider);

        return queryable;
    }
}