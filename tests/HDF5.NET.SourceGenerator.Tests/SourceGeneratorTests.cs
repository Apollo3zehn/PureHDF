using Xunit;

namespace HDF5.NET.SourceGenerator.Tests;

[H5SourceGenerator(filePath: SourceGeneratorTests.FILE_PATH)]
internal partial class MyGeneratedH5Bindings {};

public class SourceGeneratorTests
{
    internal const string FILE_PATH = "testfiles/test.h5";

    [Fact]
    public void CanGenerateSource()
    {
        using var h5File = H5File.OpenRead(FILE_PATH);
        
        var bindings = new MyGeneratedH5Bindings(h5File);
        var root = bindings.root.acquisition_information;
    }
}