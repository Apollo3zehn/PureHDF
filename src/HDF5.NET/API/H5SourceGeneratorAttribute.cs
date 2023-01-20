namespace HDF5.NET;

using System;

/// <summary>
/// Indicates that the attributed partial class should be extended with generated bindings for the specified file.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class H5SourceGeneratorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5SourceGeneratorAttribute"/> instance.
    /// </summary>
    /// <param name="filePath">The path of the file to generate the bindings for.</param>
    public H5SourceGeneratorAttribute(string filePath)
    {
        FilePath = filePath;
    }

    internal string FilePath { get; }
}