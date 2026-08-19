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
    /// <param name="options">Options to control decoding behavior.</param>
    public static NativeFile Open(
        string filePath,
        FileMode mode,
        FileAccess fileAccess,
        FileShare fileShare,
        H5ReadOptions? options = default)
    {
        return NativeFile.InternalOpen(
            filePath,
            mode,
            fileAccess,
            fileShare,
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
        return NativeFile.InternalOpen(
            CreateDriver(stream, leaveOpen),
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

    /// <summary>
    /// Opens an HDF5 file from an in-memory byte buffer.
    /// </summary>
    /// <param name="source">A read-only view of the bytes of an HDF5 file. The caller owns this buffer and may reuse or reclaim it after the returned <see cref="NativeFile"/> is disposed; the driver never writes to it.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <remarks>
    /// Reads from an in-memory buffer never suspend, so this overload always completes synchronously
    /// and the returned file's async read members complete without yielding. Concurrency is available:
    /// a per-operation driver carries its own position over the same buffer, so a dataset or attribute
    /// resolved once can be read from several threads through a single <c>H5File</c>.
    /// </remarks>
    public static NativeFile Open(
        ReadOnlyMemory<byte> source,
        H5ReadOptions? options = default)
    {
        return NativeFile.InternalOpen(
            new H5MemoryDriver(source),
            absoluteFilePath: string.Empty,
            options: options);
    }

    /// <summary>
    /// Opens an HDF5 file from a concurrent stream that reads by absolute offset.
    /// </summary>
    /// <param name="stream">
    /// The concurrent stream to read from. It must be concurrency-safe across its two read methods.
    /// </param>
    /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <remarks>
    /// Unlike the <see cref="Open(Stream, bool, H5ReadOptions?)" /> overload, this one does not
    /// require a <see cref="Stream" /> base: any class implementing <see cref="IConcurrentStream" />
    /// can be used directly. The stream is driven positionlessly - no cursor is shared, reads carry
    /// their own offsets - so concurrent reads through a single <c>H5File</c> are safe once the
    /// object has been resolved.
    /// </remarks>
    public static NativeFile Open(
        IConcurrentStream stream,
        bool leaveOpen = false,
        H5ReadOptions? options = default)
    {
        return NativeFile.InternalOpen(
            CreateDriver(stream, leaveOpen),
            absoluteFilePath: string.Empty,
            options: options);
    }

    /* ASYNCHRONOUS OPEN
     *
     * Opening a file is not a cheap metadata-free operation: it reads the superblock, walks the
     * superblock extension where present, and decodes the root group's object header. So on a source
     * that cannot be read synchronously - an HTTP range-request stream in a single-threaded WebAssembly
     * runtime - the synchronous overloads above cannot get as far as returning a file at all, and the
     * async surface on the returned object is unreachable. These are the entry points for that case.
     *
     * NativeFile is fully asynchronous internally; the synchronous overloads above bridge it with
     * GetAwaiter().GetResult(). These async overloads simply expose that internal machinery
     * directly.
     */

    /// <summary>
    /// Opens an HDF5 file for reading asynchronously. Please see the <seealso href="https://learn.microsoft.com/en-us/dotnet/api/system.io.file.openread#remarks">Remarks</seealso> section for more information how the file is opened.
    /// </summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    public static Task<NativeFile> OpenReadAsync(
        string filePath,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenReadAsync(filePath, options: options)
            .AsTask();
    }

    /// <summary>
    /// Opens an HDF5 file asynchronously.
    /// </summary>
    /// <param name="filePath">The path of the file to open.</param>
    /// <param name="mode">A <see cref="FileMode"/> value that specifies whether a file is created if one does not exist, and determines whether the contents of existing files are retained or overwritten.</param>
    /// <param name="fileAccess">A <see cref="FileAccess"/> value that specifies the operations that can be performed on the file.</param>
    /// <param name="fileShare">A <see cref="FileShare"/> value specifying the type of access other threads have to the file.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    public static Task<NativeFile> OpenAsync(
        string filePath,
        FileMode mode,
        FileAccess fileAccess,
        FileShare fileShare,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenAsync(filePath, mode, fileAccess, fileShare, options: options)
            .AsTask();
    }

    /// <summary>
    /// Opens an HDF5 stream asynchronously.
    /// </summary>
    /// <param name="stream">The stream to use. It must be readable and seekable.</param>
    /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <remarks>
    /// This is the overload that matters for a remote source. Implement <see cref="IConcurrentStream"/> on
    /// the stream as well: without it the driver falls back to the stream's own cursor, which cannot be
    /// shared between concurrent reads and does not receive the read-coalescing that makes a
    /// round-trip-bound source usable. A class that does not inherit from <see cref="Stream"/> at all
    /// can be opened through the <see cref="OpenAsync(IConcurrentStream, bool, H5ReadOptions?, CancellationToken)"/>
    /// overload instead.
    /// </remarks>
    public static Task<NativeFile> OpenAsync(
        Stream stream,
        bool leaveOpen = false,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenAsync(
                CreateDriver(stream, leaveOpen),
                absoluteFilePath: string.Empty,
                options: options)
            .AsTask();
    }

    /// <summary>
    /// Opens an HDF5 memory-mapped file asynchronously.
    /// </summary>
    /// <param name="accessor">The memory-mapped accessor to use.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <remarks>
    /// Provided for symmetry rather than for concurrency: a memory-mapped view is a synchronous source,
    /// so this always completes without suspending. It exists so that a caller written entirely against
    /// the asynchronous surface does not have to special-case one driver.
    /// </remarks>
    public static Task<NativeFile> OpenAsync(
        MemoryMappedViewAccessor accessor,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenAsync(
                new H5MemoryMappedFileDriver(accessor),
                absoluteFilePath: string.Empty,
                options: options)
            .AsTask();
    }

    /// <summary>
    /// Opens an HDF5 file asynchronously from an in-memory byte buffer.
    /// </summary>
    /// <param name="source">A read-only view of the bytes of an HDF5 file. The caller owns this buffer and may reuse or reclaim it after the returned <see cref="NativeFile"/> is disposed; the driver never writes to it.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <remarks>
    /// Provided for symmetry rather than for concurrency: an in-memory buffer is a synchronous source,
    /// so this always completes without suspending. It exists so that a caller written entirely against
    /// the asynchronous surface does not have to special-case one driver.
    /// </remarks>
    public static Task<NativeFile> OpenAsync(
        ReadOnlyMemory<byte> source,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenAsync(
                new H5MemoryDriver(source),
                absoluteFilePath: string.Empty,
                options: options)
            .AsTask();
    }

    /// <summary>
    /// Opens an HDF5 file asynchronously from a concurrent stream that reads by absolute offset.
    /// </summary>
    /// <param name="stream">
    /// The concurrent stream to read from. It must be concurrency-safe across its two read methods.
    /// </param>
    /// <param name="leaveOpen">A boolean which indicates if the stream should be kept open when this class is disposed. The default is <see langword="false"/>.</param>
    /// <param name="options">Options to control decoding behavior.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <remarks>
    /// This is the WASM-critical overload for a remote source: opening a file reads the superblock,
    /// walks object headers and decodes the root group, and on a source that cannot complete
    /// synchronously the synchronous <see cref="Open(IConcurrentStream, bool, H5ReadOptions?)" />
    /// overload cannot get as far as returning a file at all. Unlike the
    /// <see cref="OpenAsync(Stream, bool, H5ReadOptions?, CancellationToken)" /> overload, no
    /// <see cref="Stream" /> base is required.
    /// </remarks>
    public static Task<NativeFile> OpenAsync(
        IConcurrentStream stream,
        bool leaveOpen = false,
        H5ReadOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return NativeFile
            .InternalOpenAsync(
                CreateDriver(stream, leaveOpen),
                absoluteFilePath: string.Empty,
                options: options)
            .AsTask();
    }

    /// <summary>
    /// Picks the driver for <paramref name="stream"/>, shared by the synchronous and asynchronous stream
    /// overloads so that the two cannot drift apart in which driver they choose.
    /// </summary>
    private static H5DriverBase CreateDriver(Stream stream, bool leaveOpen)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new Exception("The stream must be readble and seekable.");

        // A FileStream is unwrapped to the handle driver deliberately: it reads positionally, so it
        // isolates a cursor per operation and never touches the FileStream's own.
        if (stream is FileStream fileStream)
            return new H5FileHandleDriver(fileStream, leaveOpen: leaveOpen);

        return new H5StreamDriver(stream, leaveOpen: leaveOpen);
    }

    /// <summary>
    /// Picks the driver for an <see cref="IConcurrentStream"/>. There is only one choice -
    /// positionless mode - since that is what the interface contract provides.
    /// </summary>
    private static H5DriverBase CreateDriver(IConcurrentStream stream, bool leaveOpen)
    {
        return new H5StreamDriver(stream, leaveOpen: leaveOpen);
    }
}