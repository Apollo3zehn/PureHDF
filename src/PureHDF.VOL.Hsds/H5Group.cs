using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class H5Group : H5AttributableObject, IH5Group
{
    // only for HsdsConnector super class
    public H5Group(IHsdsClient client)
        : base(name: "/")
    {
        Connector = (InternalHsdsConnector)this;

        var domain = client.Domain.GetDomain(domain: Connector.DomainName);
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

    public IEnumerable<IH5Object> Children
        => GetChildren();

    // TODO: LinkAccess seems to be quite useless for the following methods
    public bool LinkExists(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5Object Get(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5Group Group(string path, H5LinkAccess linkAccess = default)
    {
        if (path == "/")
            return Connector;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? (H5Group)Connector : this;

        for (int i = 0; i < segments.Length; i++)
        {
            try
            {
                var key = new CacheEntryKey(current.Id, segments[i]);

                current = Connector.Cache.GetOrAdd(key, key =>
                {
                    var link = Connector.Client.Link
                        .GetLink(id: key.ParentId, linkname: key.LinkName, domain: Connector.DomainName);

                    return new H5Group(Connector, link);
                });
            }
            catch (HsdsException hsds) when (hsds.StatusCode == "H00.404")
            {
                throw new Exception($"Could not find part of the path '{path}'.");
            }
        }

        return current;
    }
        
    public IH5Dataset Dataset(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5CommitedDatatype CommitedDatatype(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IEnumerable<IH5Object> GetChildren(H5LinkAccess linkAccess = default)
    {
        var response = Connector.Client.Link.GetLinks(Id, Connector.DomainName);

        return response.Links.Select(link =>
        {
            return (IH5Object)(link.Collection switch
            {
                "groups" => new H5Group(Connector, link),
                "datasets" => new H5Dataset(),
                // https://github.com/HDFGroup/hdf-rest-api/blob/e6f1a685c34ce4db68cdbdbcacacd053176a0136/openapi.yaml#L804-L805
                _ => throw new Exception("The link collection type {} is not supported. Please contact the library maintainer to enable support for this type of collection.")
            });
        });
    }
}