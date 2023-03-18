using System.Diagnostics;
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
            var expected = "g1";

            // Act
            var sw = Stopwatch.StartNew();

                #error Cache is not working?

            var actual = root
                .Group($"/{expected}")
                .Name;
            var b = sw.Elapsed.TotalMilliseconds;

            var actual2 = root
                .Group($"/{expected}")
                .Name;
            
            var c = sw.Elapsed.TotalMilliseconds;


            // Assert
            Assert.Equal(expected, actual);
        }
    }
}