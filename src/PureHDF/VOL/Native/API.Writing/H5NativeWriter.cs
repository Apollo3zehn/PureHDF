using PureHDF.Selections;

namespace PureHDF;

/// <summary>
/// A writer for HDF5 files.
/// </summary>
public partial class H5NativeWriter : IDisposable
{
    /// <summary>
    /// Write data to the specified dataset.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="dataset">The dataset to write data to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="memorySelection">The memory selection.</param>
    /// <param name="fileSelection">The file selection.</param>
    public void Write<T>(
        H5Dataset<T> dataset,
        T data,
        Selection? memorySelection = default,
        Selection? fileSelection = default)
    {
        if (!Context.DatasetToInfoMap.TryGetValue(dataset, out var info))
            throw new Exception("The provided dataset does not belong to this file.");

        var (elementType, _) = WriteUtils.GetElementType(dataset.Type);

        // TODO cache this
        var method = _methodInfoWriteDataset.MakeGenericMethod(dataset.Type, elementType);

        method.Invoke(this, new object?[]
        {
            info.H5D,
            info.Encode,
            data,
            memorySelection,
            fileSelection
        });
    }

    /// <summary>
    /// The associated <see cref="H5File"/> instance.
    /// </summary>
    public H5File File { get; }

    #region IDisposable

    private bool _disposedValue;

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // chunk indexes
                foreach (var entry in Context.DatasetToInfoMap)
                {
                    entry.Value.H5D.Dispose();
                }

                // superblock
                var endOfFileAddress = (ulong)Context.Driver.Length;

                var superblock = new Superblock23(
                    Driver: default!,
                    Version: 3,
                    FileConsistencyFlags: default,
                    BaseAddress: 0,
                    ExtensionAddress: Superblock.UndefinedAddress,
                    EndOfFileAddress: endOfFileAddress,
                    RootGroupObjectHeaderAddress: _rootGroupAddress)
                {
                    OffsetsSize = sizeof(ulong),
                    LengthsSize = sizeof(ulong)
                };

                Context.Driver.Seek(0, SeekOrigin.Begin);
                superblock.Encode(Context.Driver);

                // close driver
                Context.Driver.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}