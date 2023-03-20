using Hsds.Api;

namespace PureHDF.VOL.Hsds;

/// <inheritdoc />
public static class HsdsConnector
{
    /// <summary>
    /// Initializes a new instances of the <see cref="IHsdsConnector" />.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <param name="client">The HsdsClient used to communicate with HSDS.</param>
    public static IHsdsConnector Create(string domainName, HsdsClient client)
    {
        var domain = client.Domain.GetDomain(domainName);
        return new InternalHsdsConnector(domainName, domain.Root, client);
    }
}