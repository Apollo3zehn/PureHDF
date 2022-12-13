using HDF.PInvoke;
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
                        TestUtils.AddSomeLinks(fileId);
                });

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                string filePath;

                if (withEmptyFile)
                    filePath = TestUtils.PrepareTestFile(version, fileId => { });
                else
                    filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMassLinks(fileId));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSomeLinks(fileId));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var group = root.Group(path);

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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var group = root.Group("mass_links");

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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddSomeLinks(fileId));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var group = root.Dataset(path);

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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("mass_attributes");
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
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var parent = root.Group("mass_attributes");
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);

                var dataset_hard_1 = root.Dataset("links/hard_link_1/dataset");
                var dataset_hard_2 = root.Dataset("links/hard_link_2/dataset");
                var dataset_soft_2 = root.Dataset("links/soft_link_2/dataset");
                var dataset_direct = root.Dataset("links/dataset");
            });
        }

        [Theory]
        [InlineData("absolute", "", "")] // direct access
        [InlineData("relative", "single", "")] // use environment variable
        //[InlineData("relative", "multiple", "")] // this test will fail on Windows because of colon in path
        [InlineData("relative", "", "yes")] // use link access property list
        [InlineData("relative", "", "")] // file sits next to calling file
        [InlineData("relativecd", "", "")] // file sits next to current directory
        public void CanFollowExternalLink(string externalFilePath, string environment, string prefix)
        {
            // Arrange
            if (externalFilePath == "absolute")
            {
                externalFilePath = Path.GetTempFileName();
            }

            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, fileId => TestUtils.AddExternalFileLink(fileId, externalFilePath));

            if (externalFilePath == "relative")
                externalFilePath = Path.Combine(Path.GetTempPath(), externalFilePath);
            else if (externalFilePath == "relativecd")
                externalFilePath = Path.Combine(Environment.CurrentDirectory, externalFilePath);

            if (environment == "single")
            {
                environment = Path.GetDirectoryName(externalFilePath);
                Environment.SetEnvironmentVariable("HDF5_EXT_PREFIX", environment);
            }
            else if (environment == "multiple")
            {
                // Why did HDF Group choose a colon as prefix separator? This test must fail.
                environment = $"::C:\\temp:{Path.GetDirectoryName(externalFilePath)}";
                Environment.SetEnvironmentVariable("HDF5_EXT_PREFIX", environment);
            }

            if (prefix == "yes")
                prefix = Path.GetDirectoryName(externalFilePath);

            long res;

            var externalFileId = H5F.create(externalFilePath, H5F.ACC_TRUNC);
            var externalGroupId1 = H5G.create(externalFileId, "external");
            var externalGroupId2 = H5G.create(externalGroupId1, "group");

            var spaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(externalGroupId2, "Hello from external file =)", H5T.NATIVE_UINT, spaceId);

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(externalGroupId2);
            res = H5G.close(externalGroupId1);
            res = H5F.close(externalFileId);

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);

            var linkAccess = string.IsNullOrWhiteSpace(prefix) 
                ? new H5LinkAccess()
                : new H5LinkAccess() { ExternalLinkPrefix = prefix };

            var dataset = root.Dataset("/links/external_link/Hello from external file =)", linkAccess);
        }

        [Fact]
        public void GetsH5UnresolvedLinkForDanglingLinks()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, fileId => TestUtils.AddExternalFileLink(fileId, "not-existing.h5"));

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var link = root.Get("/links/external_link") as H5UnresolvedLink;

            // Assert
            Assert.NotNull(link);
            Assert.Equal("Unable to open external file 'not-existing.h5'.", link.Reason.Message);
        }

        [Fact]
        public void CanDerefenceWithCircularReferences()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddCircularReference(fileId));
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var value = root.Group("/circular/child/rainbow's end").Reference.Value;
                var groupReference = new H5ObjectReference() { Value = value };

                // Act
                var group = root.Get(groupReference);

                // Assert
                Assert.Equal("rainbow's end", group.Name);
            });
        }
    }
}