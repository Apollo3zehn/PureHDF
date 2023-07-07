using Hsds.Api;
using PureHDF.VOL.Hsds;

namespace PureHDF.Tests.Reading.VOL
{
    public class HsdsTestsFixture
    {
        public HsdsTestsFixture()
        {
            var domainName = "/shared/tall.h5";
            var client = new HsdsClient(new Uri("http://hsdshdflab.hdfgroup.org"));
            Connector = HsdsConnector.Create(domainName, client);
        }

        public IHsdsConnector Connector { get; }
    }
}