using System.IO.MemoryMappedFiles;

namespace PureHDF;

/// <summary>
/// An HDF5 file object. This is the entry-point to work with HDF5 files.
/// </summary>
public class H5File
{
    /// <summary>
    /// The default chunk cache factory.
    /// </summary>
    public static Func<IChunkCache> DefaultChunkCacheFactory { get; } = () => new SimpleChunkCache();

    /// <summary>
    /// Opens an HDF5 file for reading. Please see the <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.io.file.openread#remarks">Remarks</seealso> section for more information how the file is opened.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    public static IH5File OpenRead(string filePath)
    {
        return NativeH5File.OpenRead(filePath);
    }

    /// <summary>
    /// Opens an HDF5 file.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="mode">A <see cref="FileMode"/> value that specifies whether a file is created if one does not exist, and determines whether the contents of existing files are retained or overwritten.</param>
    /// <param name="fileAccess">A <see cref="FileAccess"/> value that specifies the operations that can be performed on the file.</param>
    /// <param name="fileShare">A <see cref="FileShare"/> value specifying the type of access other threads have to the file.</param>
    /// <param name="useAsync">A boolean which indicates if the file be opened with the <see cref="FileOptions.Asynchronous"/> flag.</param>
    public static IH5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool useAsync = false)
    {
        return NativeH5File.Open(filePath, mode, fileAccess, fileShare, useAsync: useAsync);
    }

    /// <summary>
    /// Opens an HDF5 stream.
    /// </summary>
    /// <param name="stream">The stream to use.</param>
    /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
    /// <returns></returns>
    public static IH5File Open(Stream stream, bool leaveOpen = false)
    {
        H5DriverBase driver;

#if NET6_0_OR_GREATER
        if (stream is FileStream fileStream)
            driver = new H5FileStreamDriver(fileStream, leaveOpen: leaveOpen);

        else
#endif
            driver = new H5StreamDriver(stream, leaveOpen: leaveOpen);

        return NativeH5File.Open(driver, string.Empty);
    }

    /// <summary>
    /// Opens an HDF5 memory-mapped file.
    /// </summary>
    /// <param name="accessor">The memory-mapped accessor to use.</param>
    /// <returns></returns>
    public static IH5File Open(MemoryMappedViewAccessor accessor)
    {
        var driver = new H5MemoryMappedFileDriver(accessor);
        return NativeH5File.Open(driver, string.Empty);
    }
}