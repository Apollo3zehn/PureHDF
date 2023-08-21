namespace PureHDF;

/// <summary>
/// A dataset.
/// </summary>
public class H5Dataset : H5AttributableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class.
    /// </summary>
    /// <param name="data">The dataset data.</param>
    /// <param name="dimensions">The dataset dimensions.</param>
    /// <param name="chunks">The dataset's chunk dimensions.</param>
    /// <param name="memorySelection">The memory selection.</param>
    /// <param name="fileSelection">The file selection.</param>
    /// <param name="datasetCreation">The dataset creation properties.</param>
    public H5Dataset(
        object data,
        ulong[]? dimensions = default,
        uint[]? chunks = default,
        Selection? memorySelection = default,
        Selection? fileSelection = default,
        H5DatasetCreation datasetCreation = default) 
        : this(
            data.GetType(), 
            data, 
            dimensions, 
            chunks, 
            memorySelection,
            fileSelection,
            datasetCreation)
    {
        //
    }

    internal H5Dataset(
        Type type,
        object? data,
        ulong[]? dimensions,
        uint[]? chunks,
        Selection? memorySelection,
        Selection? fileSelection,
        H5DatasetCreation datasetCreation)
    {
        Type = type;
        Data = data;
        Dimensions = dimensions;
        Chunks = chunks;
        MemorySelection = memorySelection;
        FileSelection = fileSelection;
        DatasetCreation = datasetCreation;
    }

    internal Type Type { get; init; }

    internal object? Data { get; }
    
    internal ulong[]? Dimensions { get; }

    internal uint[]? Chunks { get; }

    internal Selection? MemorySelection { get; }
    
    internal Selection? FileSelection { get; }

    internal H5DatasetCreation DatasetCreation { get; }
}

/// <summary>
/// A dataset.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
public class H5Dataset<T> : H5Dataset
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class.
    /// </summary>
    /// <param name="dimensions">The dataset dimensions.</param>
    /// <param name="chunks">The dataset's chunk dimensions.</param>
    /// <param name="datasetCreation">The dataset creation properties.</param>
    public H5Dataset(
        ulong[] dimensions,
        uint[]? chunks = default,
        H5DatasetCreation datasetCreation = default)
        : base(
            typeof(T), 
            default, 
            dimensions, 
            chunks, 
            default,
            default,
            datasetCreation)
    {
        //
    }
}