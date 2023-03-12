using PureHDF.VOL.Hsds;

namespace PureHDF.VOL;

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
        return new InternalHsdsConnector(domainName, client);
    }
}

internal class InternalHsdsConnector : H5Group, IHsdsConnector
{   
    public InternalHsdsConnector(string domainName, HsdsClient client) : base(domainName, client)
    {
        //
    }

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