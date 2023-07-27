namespace PureHDF;

public partial class H5File
{
    /// <summary>
    /// Creates a new file, write the contents to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The name of the file.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public void Save(string filePath, H5SerializerOptions? options = default)
    {
        H5Writer.Serialize(this, filePath, options ?? new H5SerializerOptions());
    }
}