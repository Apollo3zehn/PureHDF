namespace PureHDF;

public partial class H5File : H5Group
{
    /// <summary>
    /// Creates a new file, write the contents to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write the contents into.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public void Write(string filePath, H5WriteOptions? options = default)
    {
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var writer = new H5NativeWriter(this, fileStream, options ?? new H5WriteOptions(), leaveOpen: false);

        writer.Write();
    }

    /// <summary>
    /// Writes the contents to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write the contents into. It must be readable, writeable and seekable.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public void Write(Stream stream, H5WriteOptions? options = default)
    {
        using var writer = new H5NativeWriter(this, stream, options ?? new H5WriteOptions(), leaveOpen: true);
        writer.Write();
    }

    /// <summary>
    /// Creates a new file, write the contents to the file and returns a writer which allows to write more data to the file until the writer gets disposed. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The path of the file to write the contents into.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public H5NativeWriter BeginWrite(string filePath, H5WriteOptions? options = default)
    {
        var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var writer = new H5NativeWriter(this, fileStream, options ?? new H5WriteOptions(), leaveOpen: false);

        writer.Write();

        return writer;
    }

    /// <summary>
    /// Writes the contents to the specified stream and returns a writer which allows to write more data to the stream until the writer gets disposed.
    /// </summary>
    /// <param name="stream">The stream to write the contents into. It must be readable, writeable and seekable.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public H5NativeWriter BeginWrite(Stream stream, H5WriteOptions? options = default)
    {
        var writer = new H5NativeWriter(this, stream, options ?? new H5WriteOptions(), leaveOpen: true);
        writer.Write();

        return writer;
    }
}