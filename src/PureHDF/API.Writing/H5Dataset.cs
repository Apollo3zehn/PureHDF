using PureHDF.Selections;

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
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Dataset(
        object data,
        uint[]? chunks = default,
        Selection? memorySelection = default,
        Selection? fileSelection = default,
        ulong[]? fileDims = default,
        H5DatasetCreation datasetCreation = default,
        H5OpaqueInfo? opaqueInfo = default)
        : this(
            data.GetType(),
            data,
            chunks,
            memorySelection,
            fileSelection,
            fileDims,
            datasetCreation,
            opaqueInfo,
            isNullDataspace: false)
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
        H5DatasetCreation datasetCreation,
        H5OpaqueInfo? opaqueInfo,
        bool isNullDataspace)
    {
        Type = type;
        Data = data;
        Chunks = chunks;
        MemorySelection = memorySelection;
        FileSelection = fileSelection;
        FileDims = fileDims;
        DatasetCreation = datasetCreation;
        OpaqueInfo = opaqueInfo;
        IsNullDataspace = isNullDataspace;
    }

    internal Type Type { get; }

    internal object? Data { get; }

    internal uint[]? Chunks { get; }

    internal Selection? MemorySelection { get; }

    internal Selection? FileSelection { get; }

    internal ulong[]? FileDims { get; }

    internal H5DatasetCreation DatasetCreation { get; }

    internal H5OpaqueInfo? OpaqueInfo { get; }

    internal bool IsNullDataspace { get; }
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
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Dataset(
        ulong[] fileDims,
        uint[]? chunks = default,
        H5DatasetCreation datasetCreation = default,
        H5OpaqueInfo? opaqueInfo = default)
        : base(
            typeof(T),
            data: default,
            chunks,
            memorySelection: default,
            fileSelection: default,
            fileDims,
            datasetCreation,
            opaqueInfo,
            isNullDataspace: false)
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class. Use this constructor to preserve specific type information, e.g. for nullable value types like `int?`.
    /// </summary>
    /// <param name="data">The dataset data.</param>
    /// <param name="chunks">The dataset's chunk dimensions.</param>
    /// <param name="fileDims">The dimensions of the dataset when written to the file.</param>
    /// <param name="datasetCreation">The dataset creation properties.</param>
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Dataset(
        T data,
        uint[]? chunks = default,
        ulong[]? fileDims = default,
        H5DatasetCreation datasetCreation = default,
        H5OpaqueInfo? opaqueInfo = default)
        : base(
            typeof(T),
            data,
            chunks,
            memorySelection: default,
            fileSelection: default,
            fileDims,
            datasetCreation,
            opaqueInfo,
            isNullDataspace: false)
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class with a null dataspace.
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    /// </summary>
    public H5Dataset(
        H5OpaqueInfo? opaqueInfo = default
    )
        : base(
            typeof(T),
            data: default,
            chunks: default,
            memorySelection: default,
            fileSelection: default,
            fileDims: default,
            datasetCreation: default,
            opaqueInfo: opaqueInfo,
            isNullDataspace: true)
    {
        //
    }
}