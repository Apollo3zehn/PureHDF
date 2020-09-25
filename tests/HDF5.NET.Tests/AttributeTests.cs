using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class AttributeTests
    {
        private readonly ITestOutputHelper _logger;

        public AttributeTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        public static IList<object[]> AttributeNumericalTestData = TestData.AttributeNumericalTestData;

        [Theory]
        [MemberData(nameof(AttributeTests.AttributeNumericalTestData))]
        public void CanReadAttribute_Numerical<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.GetGroup("typed").GetAttribute(name);
                var actual = attribute.Read<T>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadAttribute_NonNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.GetGroup("typed").GetAttribute("A14");
                var actual = attribute.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.NonNullableTestStructData));
            });
        }

        [Fact]
        public void CanReadAttribute_NullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.GetGroup("typed").GetAttribute("A15");

                Func<FieldInfo, string> converter = fieldInfo =>
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute != null ? attribute.Name : fieldInfo.Name;
                };

                var actual = attribute.ReadCompound<TestStructString>(converter);

                // Assert
                Assert.True(actual.SequenceEqual(TestData.StringTestStructData));
            });
        }

        [Fact]
        public void CanReadAttribute_Tiny()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTinyAttribute(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("tiny");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.TinyData));
            });
        }

        [Fact]
        public void CanReadAttribute_Huge()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddHugeAttribute(fileId, version));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("large");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.HugeData[0..actual.Length]));
            });
        }

        [Fact]
        public void CanReadAttribute_MassAmount()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));
                var expectedCount = 1000;

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("mass_attributes");
                var attributes = parent.Attributes.ToList();

                foreach (var attribute in attributes)
                {
                    var actual = attribute.ReadCompound<TestStructL1>();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestData.NonNullableTestStructData));
                }

                Assert.Equal(expectedCount, attributes.Count);
            });
        }

        // Fixed-length string attribute (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("A11", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A12", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("A13", new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadAttribute_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.GetGroup("typed").GetAttribute(name);
                var actual = attribute.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTypedAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var attribute = root.GetGroup("typed").GetAttribute("A15");
                var exception = Assert.Throws<Exception>(() => attribute.ReadCompound<TestStructStringL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }
    }
}