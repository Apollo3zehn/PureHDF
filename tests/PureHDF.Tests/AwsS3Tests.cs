using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class AwsS3Tests
    {
        [Fact]
        public async Task CanReadAwsFile()
        {
            // Arrange
            var credentials = new AnonymousAWSCredentials();
            var region = RegionEndpoint.USWest2;
            var client = new AmazonS3Client(credentials, region);

            // https://registry.opendata.aws/nrel-pds-wtk/
            var metadata = await client.GetObjectMetadataAsync(bucketName: "nrel-pds-wtk", "Hawaii/Hawaii_2019.h5");

            
        }
    }
}