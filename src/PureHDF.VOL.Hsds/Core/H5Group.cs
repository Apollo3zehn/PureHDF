using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class H5Group : H5AttributableObject, IH5Group
{
    // only for HsdsConnector super class
    public H5Group(string domainName, IHsdsClient client)
        : base(name: "/")
    {
        Connector = (InternalHsdsConnector)this;

        var domain = client.Domain.GetDomain(domainName);
        Id = domain.Root;
    }

    public H5Group(InternalHsdsConnector connector, GetLinkResponse response) 
        : base(name: response.Link.Title)
    {
        Connector = connector;

        var link = response.Link;

        if (link.Collection != "groups")
            throw new Exception($"The provided object is not a group.");

        Id = link.Id;
    }

    // TODO: This constructor is only required because the HDF Group's openapi.json 
    // defines two differnt types for link responses.
    public H5Group(InternalHsdsConnector connector, GetLinksResponseLinksType response) 
        : base(name: response.Title)
    {
        Connector = connector;

        var link = response;

        if (link.Collection != "groups")
            throw new Exception($"The provided object is not a group.");

        Id = link.Id;
    }

    public string Id { get; }

    public InternalHsdsConnector Connector { get; }

    // TODO: LinkAccess seems to be quite useless for the following methods
    public bool LinkExists(string path) => throw new NotImplementedException();

    public Task<bool> LinkExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IH5Object Get(string path) => throw new NotImplementedException();

    public Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IH5Object Get(H5ObjectReference reference) => throw new NotImplementedException();

    public Task<IH5Object> GetAsync(H5ObjectReference reference, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IH5Group Group(string path)
    {
        return GetGroupAsync(path, useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<IH5Group> GroupAsync(string path, CancellationToken cancellationToken = default)
    {
        return GetGroupAsync(path, useAsync: true, cancellationToken);
    }
        
    public IH5Dataset Dataset(string path) => throw new NotImplementedException();

    public Task<IH5Dataset> DatasetAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IH5CommitedDatatype CommitedDatatype(string path) => throw new NotImplementedException();

    public Task<IH5CommitedDatatype> CommitedDatatypeAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IH5Object> Children()
    {
        var response = Connector.Client.Link.GetLinks(Id, Connector.DomainName);

        return response.Links.Select(link =>
        {
            return (IH5Object)(link.Collection switch
            {
                "groups" => new H5Group(Connector, link),
                "datasets" => new H5Dataset(),
                // https://github.com/HDFGroup/hdf-rest-api/blob/e6f1a685c34ce4db68cdbdbcacacd053176a0136/openapi.yaml#L804-L805
                _ => throw new Exception($"The link collection type {link.Collection} is not supported. Please contact the library maintainer to enable support for this type of collection.")
            });
        });
    }

    public Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task<IH5Group> GetGroupAsync(string path, bool useAsync, CancellationToken cancellationToken)
    {
        if (path == "/")
            return Connector;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Connector : this;

        for (int i = 0; i < segments.Length; i++)
        {
            try
            {
                var key = new CacheEntryKey(current.Id, segments[i]);

                current = await Connector.Cache.GetOrAddAsync(key, async () =>
                {
                    GetLinkResponse link;

                    if (useAsync)
                    {
                        link = await Connector.Client.Link
                            .GetLinkAsync(id: key.ParentId, linkname: key.LinkName, domain: Connector.DomainName, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        link = Connector.Client.Link
                            .GetLink(id: key.ParentId, linkname: key.LinkName, domain: Connector.DomainName);
                    }

                    return new H5Group(Connector, link);
                }).ConfigureAwait(false);
            }
            catch (HsdsException hsds) when (hsds.StatusCode == "H00.404")
            {
                throw new Exception($"Could not find part of the path '{path}'.");
            }
        }

        return current;
    }
}