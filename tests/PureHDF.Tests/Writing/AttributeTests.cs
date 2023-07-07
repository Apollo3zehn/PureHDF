using PureHDF.Experimental;
using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    [Fact]
    public void CanWriteAttribute_Numerical()
    {
        // Arrange
        var file = new Experimental.H5File();

        foreach (var data in TestData.NumericalWriteData)
        {
            var type = data.GetType();

            file.Attributes[type.Name] = type switch
            {
                Type t when t == typeof(byte[]) => new H5Attribute<byte>((byte[])data),
                Type t when t == typeof(sbyte[]) => new H5Attribute<sbyte>((sbyte[])data),
                Type t when t == typeof(ushort[]) => new H5Attribute<ushort>((ushort[])data),
                Type t when t == typeof(short[]) => new H5Attribute<short>((short[])data),
                Type t when t == typeof(uint[]) => new H5Attribute<uint>((uint[])data),
                Type t when t == typeof(int[]) => new H5Attribute<int>((int[])data),
                Type t when t == typeof(ulong[]) => new H5Attribute<ulong>((ulong[])data),
                Type t when t == typeof(long[]) => new H5Attribute<long>((long[])data),
                Type t when t == typeof(float[]) => new H5Attribute<float>((float[])data),
                Type t when t == typeof(double[]) => new H5Attribute<double>((double[])data),
                Type t when t == typeof(TestEnum[]) => new H5Attribute<TestEnum>((TestEnum[])data),
                _ => throw new Exception($"Unsupported type {type}")
            };
        }

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("TestFiles/expected.attributetests_numerical.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }
}