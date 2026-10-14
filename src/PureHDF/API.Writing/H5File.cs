namespace PureHDF;

public partial class H5File : H5Group
{
    /// <summary>
    /// Creates a new file, write the contents to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write the contents into.</param>
    /// <param name="options">Options to control encoding behavior.</param>
    public void Write(string filePath, H5WriteOptions? options = default)
    {
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var writer = new H5NativeWriter(this, fileStream, options ?? new H5WriteOptions(), leaveOpen: false);

        writer.Write();
    }

    /// <summary>
    /// Writes the contents to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write the contents into. It must be writeable and seekable; it need not be readable.</param>
    /// <param name="options">Options to control encoding behavior.</param>
    public void Write(Stream stream, H5WriteOptions? options = default)
    {
        using var writer = new H5NativeWriter(this, stream, options ?? new H5WriteOptions(), leaveOpen: true);
        writer.Write();
    }

    /// <summary>
    /// Creates a new file, write the contents to the file and returns a writer which allows to write more data to the file until the writer gets disposed. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write the contents into.</param>
    /// <param name="options">Options to control encoding behavior.</param>
    public H5NativeWriter BeginWrite(string filePath, H5WriteOptions? options = default)
    {
        var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var writer = new H5NativeWriter(this, fileStream, options ?? new H5WriteOptions(), leaveOpen: false);

        writer.Write();

        // See BeginWrite(Stream, H5WriteOptions).
        writer.Context.GlobalHeapManager.Encode();

        return writer;
    }

    /// <summary>
    /// Writes the contents to the specified stream and returns a writer which allows to write more data to the stream until the writer gets disposed.
    /// </summary>
    /// <param name="stream">The stream to write the contents into. It must be writeable and seekable; it need not be readable.</param>
    /// <param name="options">Options to control encoding behavior.</param>
    public H5NativeWriter BeginWrite(Stream stream, H5WriteOptions? options = default)
    {
        var writer = new H5NativeWriter(this, stream, options ?? new H5WriteOptions(), leaveOpen: true);
        writer.Write();

        // The structure is complete, and with it every attribute value: written now rather than at Dispose,
        // so that nothing has to be written back into the structure later.
        writer.Context.GlobalHeapManager.Encode();

        return writer;
    }

    /// <summary>
    /// Creates a new file and returns a writer that writes it piece by piece, one appended subtree at a
    /// time. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write into.</param>
    /// <param name="options">Options to control encoding behavior. See <see cref="BeginStreaming(Stream, H5WriteOptions?)" />.</param>
    /// <exception cref="ArgumentException">
    /// The options ask for a metadata reservation, or their <see cref="H5WriteOptions.SharedObjectRegionSize" />
    /// is not greater than zero.
    /// </exception>
    public static H5StreamingWriter BeginStreaming(string filePath, H5WriteOptions? options = default)
    {
        options = H5StreamingWriter.ValidateOptions(options);

        var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

        return new H5StreamingWriter(fileStream, options, leaveOpen: false);
    }

    /// <summary>
    /// Returns a writer that writes a new file to the specified stream piece by piece, one appended
    /// subtree at a time.
    /// </summary>
    /// <remarks>
    /// Streaming always clusters structure into blocks (<see cref="H5MetadataPlacement.Aggregated" />),
    /// whatever <see cref="H5WriteOptions.MetadataPlacement" /> says: interleaving would scatter structure
    /// through the payload, and front loading needs the whole structure before it writes any of it. A
    /// <see cref="H5WriteOptions.MetadataReservation" /> is rejected. The payload of a dataset declared without
    /// data is always allocated when it is first written (<see cref="H5WriteOptions.AllocatePayloadOnFirstWrite" />).
    /// </remarks>
    /// <param name="stream">The stream to write into. It must be writeable and seekable; it need not be readable.</param>
    /// <param name="options">Options to control encoding behavior.</param>
    /// <exception cref="ArgumentException">
    /// The options ask for a metadata reservation, or their <see cref="H5WriteOptions.SharedObjectRegionSize" />
    /// is not greater than zero.
    /// </exception>
    public static H5StreamingWriter BeginStreaming(Stream stream, H5WriteOptions? options = default)
    {
        options = H5StreamingWriter.ValidateOptions(options);

        return new H5StreamingWriter(stream, options, leaveOpen: true);
    }
}
