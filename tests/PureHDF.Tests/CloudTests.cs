using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class CloudTests
    {
        [Fact]
        public void CanReadAwsFile()
        {
            // Arrange
            var credentials = new AnonymousAWSCredentials();
            var region = RegionEndpoint.USWest2;
            var client = new AmazonS3Client(credentials, region);

            // https://registry.opendata.aws/nrel-pds-wtk/
            var stream = new AwsS3Stream(client, bucketName: "nrel-pds-wtk", key: "western_wind/western_wind_2004.h5");

            // Act
            var file = H5File.Open(stream);
            var children = file.Children.ToArray();

            // Assert
            Assert.Collection(children,
                child => Assert.Equal("capacity100m", child.Name),
                child => Assert.Equal("meta", child.Name),
                child => Assert.Equal("speed100m", child.Name),
                child => Assert.Equal("time_index", child.Name));
        }
    }
}