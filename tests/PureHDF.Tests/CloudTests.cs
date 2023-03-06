using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class CloudTestsSync /* splitted into sync and async to run them in parallel */
    {
        [Fact]
        public void CanReadAwsFile()
        {
            // Arrange
            var credentials = new AnonymousAWSCredentials();
            var region = RegionEndpoint.USWest2;
            var client = new AmazonS3Client(credentials, region);

            // https://registry.opendata.aws/nrel-pds-wtk/
            var stream = new AmazonS3Stream(client, bucketName: "nrel-pds-wtk", key: "western_wind/western_wind_2004.h5");

            var expected = new double[] { 10.7, 11.37, 11.81, 12.16, 12.65, 12.43, 11.66, 11.38, 11.15, 11.38 };

            // Act
            using var file = H5File.Open(stream);
            var children = file.Children.ToArray();

            var dataset = file.Dataset("speed100m");
            var fileSelection = new HyperslabSelection(rank: 2, starts: new ulong[] { 0, 0 }, blocks: new ulong[] { 10, 1 });
            var actual = dataset.Read<double>(fileSelection);

            // Assert
            Assert.Collection(children,
                child => Assert.Equal("capacity100m", child.Name),
                child => Assert.Equal("meta", child.Name),
                child => Assert.Equal("speed100m", child.Name),
                child => Assert.Equal("time_index", child.Name));

            Assert.True(expected.SequenceEqual(actual));
        }
    }

    public class CloudTestsAsync
    {
        [Fact]
        public async Task CanReadAwsFileAsync()
        {
            // Arrange
            var credentials = new AnonymousAWSCredentials();
            var region = RegionEndpoint.USWest2;
            var client = new AmazonS3Client(credentials, region);

            // https://registry.opendata.aws/nrel-pds-wtk/
            var stream = new AmazonS3Stream(client, bucketName: "nrel-pds-wtk", key: "western_wind/western_wind_2004.h5");

            var expected = new double[] { 10.7, 11.37, 11.81, 12.16, 12.65, 12.43, 11.66, 11.38, 11.15, 11.38 };

            // Act
            using var file = H5File.Open(stream);
            var children = file.Children.ToArray();

            var dataset = file.Dataset("speed100m");
            var fileSelection = new HyperslabSelection(rank: 2, starts: new ulong[] { 0, 0 }, blocks: new ulong[] { 10, 1 });
            var actual = await dataset.ReadAsync<double>(fileSelection);

            // Assert
            Assert.Collection(children,
                child => Assert.Equal("capacity100m", child.Name),
                child => Assert.Equal("meta", child.Name),
                child => Assert.Equal("speed100m", child.Name),
                child => Assert.Equal("time_index", child.Name));

            Assert.True(expected.SequenceEqual(actual));
        }
    }
}