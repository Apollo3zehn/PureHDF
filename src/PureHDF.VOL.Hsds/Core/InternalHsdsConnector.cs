using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class InternalHsdsConnector : H5Group, IHsdsConnector
{   
    public InternalHsdsConnector(string domainName, HsdsClient client) : base(domainName, client)
    {
        DomainName = domainName;
        Client = client;
    }

    public string DomainName { get; }

    public HsdsClient Client { get; }

    public GroupCache Cache { get; } = new();

    #region IDisposable

    private bool _disposedValue;

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Client.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}