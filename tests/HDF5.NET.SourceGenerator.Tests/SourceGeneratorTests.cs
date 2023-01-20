using Xunit;

namespace HDF5.NET.SourceGenerator.Tests;

public class SourceGeneratorTests
{
    [H5SourceGenerator(filePath: "testfiles/test.h5")]
    public partial class MyGeneratedH5Bindings {};

    [Fact]
    public void CanGenerateSource()
    {
        var bindings = new MyGeneratedH5Bindings();
    }
}