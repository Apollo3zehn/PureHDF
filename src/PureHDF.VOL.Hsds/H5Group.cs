using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class H5Group : H5AttributableObject, IH5Group
{
    private readonly string _id;

    // only for HsdsConnector super class
    public H5Group(string domainName, IHsdsClient client)
        : base(name: "/")
    {
        var domain = client.Domain.GetDomain(domain: domainName);
        _id = domain.Root;

        DomainName = domainName;
        Connector = (InternalHsdsConnector)this;
    }

    public H5Group(string domainName, InternalHsdsConnector connector, GetLinkResponse group) 
        : base(name: group.Link.Title)
    {
        var link = group.Link;

        if (link.Collection != "groups")
            throw new Exception($"The provided object is not a group.");

        _id = link.Id;

        DomainName = domainName;
        Connector = connector;
    }

    public string DomainName { get; }

    internal InternalHsdsConnector Connector { get; }

    /// <inheritdoc />
    public IH5Group Group(string path, H5LinkAccess linkAccess = default)
    {
        if (path == "/")
            return Connector;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? (IH5Group)Connector : this;

        for (int i = 0; i < segments.Length; i++)
        {
            try
            {
                var key = new CacheEntryKey(_id, segments[i]);

                current = Connector.Cache.GetOrAdd(key, key =>
                {
                    var link = Connector.Client.Link
                        .GetLink(id: key.ParentId, linkname: key.LinkName, domain: DomainName);

                    return new H5Group(DomainName, Connector, link);
                });
            }
            catch (HsdsException hsds) when (hsds.StatusCode == "H00.404")
            {
                throw new Exception($"Could not find part of the path '{path}'.");
            }
        }

        return current;
    }
        
    public IEnumerable<IH5Object> Children { get => throw new NotImplementedException(); }

    public bool LinkExists(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5Object Get(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public T Get<T>(string path, H5LinkAccess linkAccess = default) where T : IH5Object => throw new NotImplementedException();

    public IH5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public T Get<T>(H5ObjectReference reference, H5LinkAccess linkAccess = default) where T : IH5Object => throw new NotImplementedException();

    public IH5Dataset Dataset(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IH5CommitedDatatype CommitedDatatype(string path, H5LinkAccess linkAccess = default) => throw new NotImplementedException();

    public IEnumerable<IH5Object> GetChildren(H5LinkAccess linkAccess = default) => throw new NotImplementedException();
}