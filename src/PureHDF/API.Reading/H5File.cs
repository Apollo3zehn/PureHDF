using System.IO.MemoryMappedFiles;

namespace PureHDF;

/// <summary>
/// Entry-point for PureHDF.
/// </summary>
public partial class H5File
{
    /// <summary>
    /// Opens an HDF5 file for reading. Please see the <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.io.file.openread#remarks">Remarks</seealso> section for more information how the file is opened.
    /// </summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    public static NativeFile OpenRead(
        string filePath, 
        H5ReadOptions? options = default)
    {
        return NativeFile.InternalOpenRead(
            filePath,
            options: options);
    }

    /// <summary>
    /// Opens an HDF5 file.
    /// </summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="mode">A <see cref="FileMode"/> value that specifies whether a file is created if one does not exist, and determines whether the contents of existing files are retained or overwritten.</param>
    /// <param name="fileAccess">A <see cref="FileAccess"/> value that specifies the operations that can be performed on the file.</param>
    /// <param name="fileShare">A <see cref="FileShare"/> value specifying the type of access other threads have to the file.</param>
    /// <param name="useAsync">A boolean which indicates if the file be opened with the <see cref="FileOptions.Asynchronous"/> flag.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    public static NativeFile Open(
        string filePath, 
        FileMode mode, 
        FileAccess fileAccess, 
        FileShare fileShare, 
        bool useAsync = false,
        H5ReadOptions? options = default)
    {
        return NativeFile.InternalOpen(
            filePath, 
            mode, 
            fileAccess, 
            fileShare, 
            useAsync: useAsync,
            options: options);
    }

    /// <summary>
    /// Opens an HDF5 stream.
    /// </summary>
    /// <param name="stream">The stream to use. It must be readable and seekable.</param>
    /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    public static NativeFile Open(
        Stream stream, 
        bool leaveOpen = false,
        H5ReadOptions? options = default)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new Exception("The stream must be readble and seekable.");

        H5DriverBase driver;

#if NET6_0_OR_GREATER
        if (stream is FileStream fileStream)
            driver = new H5FileHandleDriver(fileStream, leaveOpen: leaveOpen);

        else
#endif
            driver = new H5StreamDriver(stream, leaveOpen: leaveOpen);

        return NativeFile.InternalOpen(
            driver, 
            absoluteFilePath: string.Empty, 
            options: options);
    }

    /// <summary>
    /// Opens an HDF5 memory-mapped file.
    /// </summary>
    /// <param name="accessor">The memory-mapped accessor to use.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    public static NativeFile Open(
        MemoryMappedViewAccessor accessor,
        H5ReadOptions? options = default)
    {
        var driver = new H5MemoryMappedFileDriver(accessor);
        
        return NativeFile.InternalOpen(
            driver, 
            absoluteFilePath: string.Empty, 
            options: options);
    }
}