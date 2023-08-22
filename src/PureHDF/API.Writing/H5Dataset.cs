using Microsoft.VisualBasic;

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
    /// <param name="chunks">The dataset's chunk dimensions.</param>
    /// <param name="memorySelection">The memory selection.</param>
    /// <param name="fileSelection">The file selection.</param>
    /// <param name="fileDims">The dimensions of the dataset when written to the file.</param>
    /// <param name="datasetCreation">The dataset creation properties.</param>
    public H5Dataset(
        object data,
        uint[]? chunks = default,
        Selection? memorySelection = default,
        Selection? fileSelection = default,
        ulong[]? fileDims = default,
        H5DatasetCreation datasetCreation = default) 
        : this(
            data.GetType(), 
            data, 
            chunks,
            memorySelection,
            fileSelection,
            fileDims, 
            datasetCreation)
    {
        //
    }

    internal H5Dataset(
        Type type,
        object? data,
        uint[]? chunks,
        Selection? memorySelection,
        Selection? fileSelection,
        ulong[]? fileDims,
        H5DatasetCreation datasetCreation)
    {
        Type = type;
        Data = data;
        Chunks = chunks;
        MemorySelection = memorySelection;
        FileSelection = fileSelection;
        FileDims = fileDims;
        DatasetCreation = datasetCreation;
    }

    internal Type Type { get; init; }

    internal object? Data { get; }

    internal uint[]? Chunks { get; }

    internal Selection? MemorySelection { get; }
    
    internal Selection? FileSelection { get; }

    internal ulong[]? FileDims { get; }

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
    /// <param name="fileDims">The dimensions of the dataset when written to the file.</param>
    /// <param name="chunks">The dataset's chunk dimensions.</param>
    /// <param name="datasetCreation">The dataset creation properties.</param>
    public H5Dataset(
        ulong[] fileDims,
        uint[]? chunks = default,
        H5DatasetCreation datasetCreation = default)
        : base(
            typeof(T), 
            default, 
            chunks, 
            default,
            default,
            fileDims, 
            datasetCreation)
    {
        //
    }
}

/// <summary>
/// A null space dataset.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
public class H5NullDataset<T> : H5Dataset
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5NullDataset{T}"/> class.
    /// </summary>
    public H5NullDataset()
        : base(
            typeof(T), 
            data: null,
            default,
            default,
            default,
            fileDims: null, 
            default)
    {
        //
    }
}