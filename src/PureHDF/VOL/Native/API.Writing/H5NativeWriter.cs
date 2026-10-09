using PureHDF.Selections;
using PureHDF.VOL.Native;

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

        // As in WriteDatasetData: variable-length elements written here are the dataset's payload,
        // so their heap collections are allocated as payload too.
        var enclosing = Context.GlobalHeapManager.AllocationKind;
        Context.GlobalHeapManager.AllocationKind = AllocationKind.RawData;

        try
        {
            method.Invoke(this,
            [
                info.H5D ?? CreateLayout(info.Info, dataset.DatasetCreation),
                info.Encode,
                data,
                memorySelection,
                fileSelection
            ]);
        }

        finally
        {
            Context.GlobalHeapManager.AllocationKind = enclosing;
        }
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
                // A write that failed before its structure was complete still leaves a file of what
                // it got through.
                PlaceStructure();

                // chunk indexes
                foreach (var entry in Context.DatasetToInfoMap)
                {
                    entry.Value.H5D?.Dispose();
                }

                // global heap collections
                Context.GlobalHeapManager.Encode();

                // superblock
                //
                // The end-of-file address must cover everything ALLOCATED, not merely everything
                // written, because an address can be referenced without ever being written to: a
                // dataset created through BeginWrite has its payload allocated and named by its layout
                // message whether or not the caller ever writes it. Nothing extends the stream over
                // that payload, and a placement decides whether anything else does - an interleaved
                // file usually has metadata allocated after it, whose writes extend the stream by
                // accident, while a front-loaded one serves metadata from the front and leaves the
                // stream ending before the payload the layout points at. h5dump then rejects the
                // dataset outright: "invalid dataset size, likely file corruption".
                //
                // So the stream is extended to the allocator's mark rather than the address being
                // trimmed to the stream. Trimming would be smaller - an abandoned region tail is
                // usually the last thing in the file, and it is referenced by nothing - but it cannot
                // be told apart from the payload case here, and getting it wrong truncates data.
                // BaseAddress is added because the allocator counts from the superblock while the
                // driver's Length and SetLength count from the start of the file, and a user block puts
                // those a UserBlockSize apart. Comparing them untranslated makes the guard read as
                // already-long-enough and skip an extension the file needs.
                var highWaterMark = (long)Context.Driver.BaseAddress + Context.FreeSpaceManager.HighWaterMark;

                if (Context.Driver.Length < highWaterMark)
                    Context.Driver.SetLength(highWaterMark);

                var endOfFileAddress = (ulong)Context.Driver.Length;

                var superblock = new Superblock23(
                    Driver: default!,
                    Version: 3,
                    FileConsistencyFlags: default,
                    BaseAddress: Context.Driver.BaseAddress,
                    ExtensionAddress: Superblock.UndefinedAddress,
                    EndOfFileAddress: endOfFileAddress,
                    RootGroupObjectHeaderAddress: _rootGroupAddress)
                {
                    OffsetsSize = sizeof(ulong),
                    LengthsSize = sizeof(ulong)
                };

                Context.Driver.SeekRelativeToBaseAddress(0);

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