namespace PureHDF.VOL.Hsds;

internal class HsdsAttributableObject : HsdsObject, IH5AttributableObject
{
    // this constructor is only for the derived InternalHsdsConnector class
    public HsdsAttributableObject(HsdsNamedReference reference) 
        : base(reference)
    {
        Connector = (InternalHsdsConnector)this;
    }

    public HsdsAttributableObject(InternalHsdsConnector connector, HsdsNamedReference reference) 
        : base(reference)
    {
        Connector = connector;
    }

    public InternalHsdsConnector Connector { get; }

    public IEnumerable<IH5Attribute> Attributes()
    {
        return GetAttributesAsync(useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<IEnumerable<IH5Attribute>> AttributesAsync(CancellationToken cancellationToken = default)
    {
        return GetAttributesAsync(useAsync: false, default);
    }

    public IH5Attribute Attribute(string name)
    {
        return GetAttributeAsync(name, useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<IH5Attribute> AttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        return GetAttributeAsync(name, useAsync: true, cancellationToken);
    }

    // TODO: should this be a Native only method? Here it relies on try/catch which is not useful.
    public bool AttributeExists(string name)
    {
        return InternalAttributeExistsAsync(name, useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<bool> AttributeExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return InternalAttributeExistsAsync(name, useAsync: true, cancellationToken);
    }

    private async Task<bool> InternalAttributeExistsAsync(
        string name, bool useAsync, CancellationToken cancellationToken)
    {
        try
        {
            await GetAttributeAsync(name, useAsync, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IH5Attribute> GetAttributeAsync(string name, bool useAsync, CancellationToken cancellationToken)
    {
        var collection = this switch
        {
            IH5Group => "groups",
            IH5Dataset => "datasets",
            IH5DataType => "datatypes",
            _ => throw new Exception($"The collection of type {this.GetType().Name} is not supported.")
        };

        var getAttributeResponse = useAsync

            ? await Connector.Client.Attribute
                .GetAttributeAsync(collection, Id, name, Connector.DomainName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                
            : Connector.Client.Attribute
                .GetAttribute(collection, Id, name, Connector.DomainName);

        // TODO: Initialize space and type lazily. See H5AttributableObject.
        // TODO: Fix this as soon as https://github.com/HDFGroup/hdf-rest-api/issues/12 is resolved
        return new HsdsAttribute(
            getAttributeResponse.Name, default!, default!);
    }

    private async Task<IEnumerable<IH5Attribute>> GetAttributesAsync(bool useAsync, CancellationToken cancellationToken)
    {
        var collection = this switch
        {
            IH5Group => "groups",
            IH5Dataset => "datasets",
            IH5DataType => "datatypes",
            _ => throw new Exception($"The collection of type {GetType().Name} is not supported.")
        };

        var getAttributesResponse = useAsync

            ? await Connector.Client.Attribute
                .GetAttributesAsync(collection, Id, Connector.DomainName, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                
            : Connector.Client.Attribute
                .GetAttributes(collection, Id, Connector.DomainName);

        // TODO: Initialize space and type lazily. See H5AttributableObject.
        // TODO: Fix this as soon as https://github.com/HDFGroup/hdf-rest-api/issues/12 is resolved
        return getAttributesResponse.Attributes
            .Select(attribute => new HsdsAttribute(attribute.Name, default!, default!));
    }
}