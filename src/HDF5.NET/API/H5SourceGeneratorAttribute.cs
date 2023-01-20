namespace HDF5.NET;

public class H5SourceGeneratorAttribute : Attribute
{
    public H5SourceGeneratorAttribute(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}