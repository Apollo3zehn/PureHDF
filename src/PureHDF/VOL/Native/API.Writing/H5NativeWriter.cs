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
    public void WriteDataset<T>(H5Dataset<T> dataset, T data)
    {
        if (!Context.DatasetToH5DMap.TryGetValue(dataset, out var h5d))
            throw new Exception("The provided dataset does not belong to this file.");

        
    }

    #region IDisposable

    private bool _disposedValue;

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
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