using PureHDF.Selections;

namespace PureHDF;

/// <summary>
/// A dataset.
/// </summary>
public class H5Dataset : H5Object
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
        object? data,
        uint[]? chunks,
        Selection? memorySelection,
        Selection? fileSelection,
        ulong[]? fileDims,
        H5DatasetCreation datasetCreation,
        H5OpaqueInfo? opaqueInfo,
        bool isNullDataspace)
    {
        Data = data;

        // A dataset of one dimension, by far the most common, keeps it inline rather than in an array.
        if (fileDims is { Length: 1 })
        {
            _fileDim0 = fileDims[0];
            _hasInlineFileDim = true;
            fileDims = default;
        }

        if (chunks is not null ||
            memorySelection is not null ||
            fileSelection is not null ||
            fileDims is not null ||
            datasetCreation != default ||
            opaqueInfo is not null ||
            isNullDataspace)
        {
            _options = new Options(
                chunks,
                memorySelection,
                fileSelection,
                fileDims,
                datasetCreation,
                opaqueInfo,
                isNullDataspace);
        }
    }

    // What a dataset is given beyond its data and its dimensions, which most datasets are not: kept
    // apart so that a dataset without any of it does not carry a field for each. A file of many small
    // datasets is mostly tree, and as fields of their own these would be half of each dataset.
    private sealed record Options(
        uint[]? Chunks,
        Selection? MemorySelection,
        Selection? FileSelection,
        ulong[]? FileDims,
        H5DatasetCreation DatasetCreation,
        H5OpaqueInfo? OpaqueInfo,
        bool IsNullDataspace);

    private readonly Options? _options;
    private readonly ulong _fileDim0;
    private readonly bool _hasInlineFileDim;

    // The data's own type, or for H5Dataset<T> its type argument, which can differ from it - the only
    // way a dataset gets one that does - so it is not stored.
    internal virtual Type Type => Data!.GetType();

    internal object? Data { get; }

    internal uint[]? Chunks => _options?.Chunks;

    internal Selection? MemorySelection => _options?.MemorySelection;

    internal Selection? FileSelection => _options?.FileSelection;

    internal ulong[]? FileDims => _hasInlineFileDim
        ? [_fileDim0]
        : _options?.FileDims;

    internal H5DatasetCreation DatasetCreation => _options?.DatasetCreation ?? default;

    internal H5OpaqueInfo? OpaqueInfo => _options?.OpaqueInfo;

    internal bool IsNullDataspace => _options?.IsNullDataspace ?? false;
}

/// <summary>
/// A dataset.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
public class H5Dataset<T> : H5Dataset
{
    internal override Type Type => typeof(T);

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