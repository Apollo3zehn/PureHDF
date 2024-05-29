namespace PureHDF.VOL.Hsds;

internal class HsdsObject : IH5Object
{
    // this constructor is only for the derived InternalHsdsConnector class
    public HsdsObject(HsdsNamedReference reference)
    {
        Reference = reference;
        Connector = (InternalHsdsConnector)this;
    }

    public HsdsObject(
        InternalHsdsConnector connector, 
        HsdsNamedReference reference)
        : this(reference)
    {
        Connector = connector;
    }

    public string Name => Reference.Title;

    public string Id => Reference.Id;

    public InternalHsdsConnector Connector { get; }

    internal HsdsNamedReference Reference { get; set; }

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

    private Task<bool> InternalAttributeExistsAsync(
        string name, bool useAsync, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
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

        var attribute = useAsync

            ? await Connector.Client.Attribute
                .GetAttributeAsync(Connector.DomainName, collection, Id, name, cancellationToken: cancellationToken)
                .ConfigureAwait(false)

            : Connector.Client.Attribute
                .GetAttribute(Connector.DomainName, collection, Id, name);

        return new HsdsAttribute(attribute);
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

        return getAttributesResponse.Attributes
            .Select(attribute => new HsdsAttribute(attribute));
    }
}