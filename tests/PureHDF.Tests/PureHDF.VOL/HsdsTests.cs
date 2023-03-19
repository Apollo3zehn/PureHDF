using Hsds.Api;
using PureHDF.VOL.Hsds;
using Xunit;

namespace PureHDF.Tests.Reading.VOL
{
    public class HsdsTests
    {
        [Fact]
        public void CanGetGroup()
        {
            // Arrange
            var domainName = "/shared/tall.h5";
            var client = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
            var root = HsdsConnector.Create(domainName, client);
            var expected = "g1.1";

            // Act
            var actual = root
                .Group($"/g1/{expected}")
                .Name;

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}