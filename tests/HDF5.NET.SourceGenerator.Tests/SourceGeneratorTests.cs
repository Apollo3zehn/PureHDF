using Xunit;

namespace HDF5.NET.SourceGenerator.Tests;

[H5SourceGenerator(filePath: SourceGeneratorTests.FILE_PATH)]
internal partial class MyGeneratedH5Bindings {};

[H5SourceGenerator(filePath: SourceGeneratorTests.FILE_PATH)]
public class MyGeneratedH5BindingsNotPartial {};

public class SourceGeneratorTests
{
    internal const string FILE_PATH = "testfiles/test.h5";

    [Fact]
    public void CanGenerateSource()
    {
        var bindings = new MyGeneratedH5Bindings();

        var root = bindings.root.acquisition_information;
    }
}