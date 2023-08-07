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
        H5Writer.Write(this, fileStream, options ?? new H5WriteOptions());
    }

    /// <summary>
    /// Writes the contents to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write the contents into. It must be readable, writeable and seekable.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public void Write(Stream stream, H5WriteOptions? options = default)
    {
        // TODO readable is only required for checksums, maybe this requirement can be lifted by renting Memory<byte> and calculate the checksum over that memory
        H5Writer.Write(this, stream, options ?? new H5WriteOptions());
    }
}