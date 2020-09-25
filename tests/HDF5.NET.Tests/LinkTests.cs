using HDF.PInvoke;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class LinkTests
    {
        private readonly ITestOutputHelper _logger;

        public LinkTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Theory]
        [InlineData("/", true, false)]
        [InlineData("/simple", true, false)]
        [InlineData("/simple/sub?!", false, false)]
        [InlineData("/simple/sub?!", false, true)]
        public void CanCheckLinkExistsSimple(string path, bool expected, bool withEmptyFile)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId =>
                {
                    if (!withEmptyFile)
                        TestUtils.AddSimple(fileId);
                });

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var actual = root.LinkExists(path);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/", true, false)]
        [InlineData("/mass_links", true, false)]
        [InlineData("/mass_links/mass_0000", true, false)]
        [InlineData("/mass_links/mass_0020", true, false)]
        [InlineData("/mass_links/mass_0102", true, false)]
        [InlineData("/mass_links/mass_0999", true, false)]
        [InlineData("/mass_links/mass_1000", false, false)]
        [InlineData("/mass_links/mass_1000", false, true)]
        public void CanCheckLinkExistsMass(string path, bool expected, bool withEmptyFile)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var actual = root.LinkExists(path);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/", "/")]
        [InlineData("/simple", "simple")]
        [InlineData("/simple/sub", "sub")]
        public void CanOpenGroup(string path, string expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSimple(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.GetGroup(path);

                // Assert
                Assert.Equal(expected, group.Name);
            });
        }

        [Fact]
        public void CanEnumerateLinksMass()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassLinks(fileId));
                var expected = 1000;

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.GetGroup("mass_links");

                // Assert
                var actual = group.Children.Count();
                Assert.Equal(expected, actual);
            });
        }

        [Theory]
        [InlineData("/D", "D")]
        [InlineData("/simple/D1", "D1")]
        [InlineData("/simple/sub/D1.1", "D1.1")]
        public void CanOpenDataset(string path, string expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSimple(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var group = root.GetDataset(path);

                // Assert
                Assert.Equal(expected, group.Name);
            });
        }

        [Theory]
        [InlineData("mass_0000", true)]
        [InlineData("mass_0020", true)]
        [InlineData("mass_0102", true)]
        [InlineData("mass_0999", true)]
        [InlineData("mass_1000", false)]
        public void CanCheckAttribute_ExistsMass(string attributeName, bool expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
                var parent = root.GetGroup("mass_attributes");
                var actual = parent.AttributeExists(attributeName);

                // Assert
                Assert.Equal(expected, actual);
            });
        }

        [Fact]
        public void CanCheckAttribute_ExistsUTF8()
        {
            // Arrange
            var version = H5F.libver_t.LATEST;
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassAttributes(fileId));

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var parent = root.GetGroup("mass_attributes");
            var actual = parent.AttributeExists("字形碼 / 字形码, Zìxíngmǎ");

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void CanFollowLinks()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);

                var dataset_hard_1 = root.GetDataset("links/hard_link_1/dataset");
                var dataset_hard_2 = root.GetDataset("links/hard_link_2/dataset");
                var dataset_soft_2 = root.GetDataset("links/soft_link_2/dataset");
                var dataset_direct = root.GetDataset("links/dataset");
            });
        }

        [Fact]
        public void CanOpenLink()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddLinks(fileId));

                // Act
                using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);

                var group = root.GetSymbolicLink("links/soft_link_2");
                var dataset = root.GetSymbolicLink("links/dataset");
            });
        }
    }
}