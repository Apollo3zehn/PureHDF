using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class InternalHsdsConnector : HsdsGroup, IHsdsConnector
{
    public InternalHsdsConnector(
        HsdsClient client, 
        HsdsNamedReference reference, 
        string domainName) : base(reference)
    {
        Client = client;
        DomainName = domainName;
    }

    public string DomainName { get; }

    public HsdsClient Client { get; }

    public ObjectCache Cache { get; } = new();

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