using System.Net.Http.Headers;
using System.Text;
using Hsds.Api;
using PureHDF.VOL.Hsds;

namespace PureHDF.Tests.Reading.VOL;

public class HsdsTestsFixture
{
    public HsdsTestsFixture()
    {
        var domainName = "/shared/tall.h5";

        var authenticationString = $"admin:admin";
        var base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));

        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri("http://localhost:5101")
        };

        httpClient.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("Basic", base64String);

        var client = new HsdsClient(httpClient);
        Connector = HsdsConnector.Create(domainName, client);
    }

    public IHsdsConnector Connector { get; }
}