using PureHDF.VOL;
using Xunit;

namespace PureHDF.Tests.Reading.VOL
{
    public class HsdsTests
    {
        [Fact]
        public async Task Dummy()
        {
            var domainName = "/shared/tall.h5";
            var hsdsClient = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
            var domain = await hsdsClient.Domain.GetDomainAsync(domain: domainName);
            var rootId = domain["root"].GetString()!;
            var links = await hsdsClient.Link.GetLinkAsync(rootId, domain: domainName);

            var b = 1;
        }
    }
}