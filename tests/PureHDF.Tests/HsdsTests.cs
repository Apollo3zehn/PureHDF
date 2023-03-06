using Xunit;

namespace PureHDF.Tests.Reading
{
    public class HsdsTests
    {
        [Fact]
        public async Task Dummy()
        {
            var hsdsClient = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
            var domain = await hsdsClient.Domain.GetInformationAboutTheRequestedDomainAsync(domain: "/shared/tall.h5");
            var rootId = domain["root"].GetString()!;
            var links = await hsdsClient.Link.ListAllLinksInAGroupAsync(rootId, domain: "/shared/tall.h5");

            var b = 1;
        }
    }
}