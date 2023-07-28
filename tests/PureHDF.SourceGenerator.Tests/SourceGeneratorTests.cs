using Xunit;

namespace PureHDF.SourceGenerator.Tests;

[H5SourceGenerator(filePath: SourceGeneratorTests.FILE_PATH)]
internal partial class MyGeneratedH5Bindings { };

public class SourceGeneratorTests
{
    internal const string FILE_PATH = "TestFiles/source_generator.h5";

    [Fact]
    public void CanGenerateSource()
    {
        using var nativeFile = H5File.OpenRead(FILE_PATH);

        // Act
        var bindings = new MyGeneratedH5Bindings(nativeFile);
        var group = bindings.group1.Get();
        var dataset = bindings.group1.sub_group1.sub_sub_dataset1;
        var actual = dataset.Read<long>();

        var expected = Enumerable
            .Range(0, 10)
            .Select(value => (long)value)
            .ToArray();

        // Assert
        Assert.NotNull(group);
        Assert.True(expected.SequenceEqual(actual));
    }
}