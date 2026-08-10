using PureHDF.Selections;
using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

/// <summary>
/// A native HDF5 file object. This is the entry-point to work with HDF5 files.
/// </summary>
public class NativeFile : NativeGroup, IDisposable
{
    // TODO: K-Values message https://forum.hdfgroup.org/t/problem-reading-version-1-8-hdf5-files-using-file-format-specification-document-clarification-needed/7568
    #region Fields

    private readonly bool _deleteOnClose;

    #endregion

    #region Constructors

    private NativeFile(
        NativeReadContext context,
        NativeNamedReference reference,
        ObjectHeader header,
        string absoluteFilePath,
        bool deleteOnClose) : base(context, reference, header)
    {
        Path = absoluteFilePath;
        FolderPath = System.IO.Path.GetDirectoryName(absoluteFilePath);
        _deleteOnClose = deleteOnClose;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the path of the opened HDF5 file if loaded from the file system.
    /// </summary>
    public string? Path { get; }

    internal string? FolderPath { get; }

    #endregion

    #region Methods

    // NOTE (async-first): the *Async variants below are the real implementation. These three
    // synchronous entry points are preserved under their original names and block on them, so the
    // existing synchronous public surface (H5File.OpenRead/Open) and every existing caller keep
    // working unchanged. Blocking here is safe on a thread-backed host; a WebAssembly caller must
    // use the *Async entry points instead, where nothing blocks.
    internal static NativeFile InternalOpenRead(
        string filePath,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        return InternalOpenReadAsync(filePath, deleteOnClose, options)
            .GetAwaiter()
            .GetResult();
    }

    internal static NativeFile InternalOpen(
        string filePath,
        FileMode fileMode,
        FileAccess fileAccess,
        FileShare fileShare,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        return InternalOpenAsync(filePath, fileMode, fileAccess, fileShare, deleteOnClose, options)
            .GetAwaiter()
            .GetResult();
    }

    internal static NativeFile InternalOpen(
        H5DriverBase driver,
        string absoluteFilePath,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        return InternalOpenAsync(driver, absoluteFilePath, deleteOnClose, options)
            .GetAwaiter()
            .GetResult();
    }

    internal static async ValueTask<NativeFile> InternalOpenReadAsync(
        string filePath,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        return await InternalOpenAsync(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            deleteOnClose: deleteOnClose,
            options: options).ConfigureAwait(false);
    }

    internal static async ValueTask<NativeFile> InternalOpenAsync(
        string filePath,
        FileMode fileMode,
        FileAccess fileAccess,
        FileShare fileShare,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        var absoluteFilePath = System.IO.Path.GetFullPath(filePath);

        var stream = new FileStream(
            absoluteFilePath,
            fileMode,
            fileAccess,
            fileShare,
            4096);

        var driver = new H5FileHandleDriver(stream, leaveOpen: false);

        return await InternalOpenAsync(
            driver,
            absoluteFilePath,
            deleteOnClose,
            options).ConfigureAwait(false);
    }

    internal static async ValueTask<NativeFile> InternalOpenAsync(
        H5DriverBase driver,
        string absoluteFilePath,
        bool deleteOnClose = false,
        H5ReadOptions? options = default)
    {
        if (!BitConverter.IsLittleEndian)
            throw new Exception("This library only works on little endian systems.");

        // superblock
        var stepSize = 512;
        var signature = await driver.ReadBytes(8).ConfigureAwait(false);

        while (!ValidateSignature(signature, Superblock.Signature))
        {
            driver.Seek(stepSize - 8, SeekOrigin.Current);

            if (driver.Position >= driver.Length)
                throw new Exception("The file is not a valid HDF 5 file.");

            signature = await driver.ReadBytes(8).ConfigureAwait(false);
            stepSize *= 2;
        }

        var version = await driver.ReadByte().ConfigureAwait(false);

        Superblock superblock = version switch
        {
            >= 0 and < 2 => await Superblock01.Decode(driver, version).ConfigureAwait(false),
            >= 2 and < 4 => await Superblock23.Decode(driver, version).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
        };

        driver.SetBaseAddress(superblock.BaseAddress);

        ulong address;
        var superblock01 = superblock as Superblock01;

        if (superblock01 is not null)
        {
            address = superblock01.RootGroupSymbolTableEntry.HeaderAddress;
        }

        else
        {
            var superblock23 = superblock as Superblock23;

            if (superblock23 is not null)
                address = superblock23.RootGroupObjectHeaderAddress;

            else
                throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
        }

        driver.SeekRelativeToBaseAddress((long)address);

        var context = new NativeReadContext(
            driver,
            superblock)
        {
            ReadOptions = options ?? new()
        };

        var header = await ObjectHeader.Construct(context).ConfigureAwait(false);

        var file = new NativeFile(context, default, header, absoluteFilePath, deleteOnClose);
        var reference = new NativeNamedReference("/", address, file);
        file.Reference = reference;

        context.File = file;

        return file;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateSignature(byte[] actual, byte[] expected)
    {
        return expected.SequenceEqual(actual);
    }

    /// <summary>
    /// Gets the file selection that is referenced by the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the region.</param>
    /// <returns>The requested selection.</returns>
    public Selection Get(NativeRegionReference1 reference)
    {
        if (reference.Equals(default))
            throw new Exception("The reference is invalid");

        Context.Driver.SeekRelativeToBaseAddress((long)reference.CollectionAddress);

        var globalHeapId = new ReadingGlobalHeapId(
            CollectionAddress: reference.CollectionAddress,
            ObjectIndex: reference.ObjectIndex);

        var globalHeapCollection = NativeCache.GetGlobalHeapObject(Context, globalHeapId.CollectionAddress);
        var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex];

        using var localDriver = new H5StreamDriver(new MemoryStream(globalHeapObject.ObjectData), leaveOpen: false);
        var address = Context.Superblock.ReadOffset(localDriver).GetAwaiter().GetResult();
        var dataspaceSelection = DataspaceSelection.Decode(localDriver).GetAwaiter().GetResult();

        Selection selection = dataspaceSelection.Info switch
        {
            H5S_SEL_NONE none => new NoneSelection(),
            H5S_SEL_POINTS points => new PointSelection(points.PointData),
            H5S_SEL_HYPER hyper => hyper.SelectionInfo switch
            {
                RegularHyperslabSelectionInfo regular => new HyperslabSelection((int)regular.Rank, regular.Starts, regular.Strides, regular.Counts, regular.Blocks),
                IrregularHyperslabSelectionInfo irregular => new IrregularHyperslabSelection((int)irregular.Rank, irregular.BlockOffsets),
                _ => throw new NotSupportedException($"The hyperslab selection type '{hyper.SelectionInfo.GetType().FullName}' is not supported.")
            },
            H5S_SEL_ALL all => new AllSelection(),
            _ => throw new NotSupportedException($"The dataspace selection type '{dataspaceSelection.Info.GetType().FullName}' is not supported.")
        };

        return selection;
    }

    #endregion

    #region IDisposable

    private bool _disposedValue;

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                NativeCache.Clear(Context);
                Context.Driver.Dispose();

                if (_deleteOnClose && File.Exists(Path))
                {
                    try
                    {
                        File.Delete(Path!);
                    }
                    catch
                    {
                        //
                    }
                }
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