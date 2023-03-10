// TODO: HSDS sync client
// domain ist not optional?! ask gheber

using System.Text.Json;
using PureHDF.VOL.Hsds;

namespace PureHDF.VOL;

internal class H5Group : H5AttributableObject, IH5Group
{
    private readonly IHsdsConnector? _connector;
    private readonly string _id;

    public H5Group(string domainName, HsdsClient client, IReadOnlyDictionary<string, JsonElement>? group = default) 
        : base(name: group is null ? "/" : group["link"].GetProperty("title")!.GetString()!)
    {
        if (group is null)
        {
            var domain = client.Domain
                .GetDomainAsync(domain: domainName)
                .GetAwaiter()
                .GetResult();

            _id = domain["root"].GetString()!;
        }

        else
        {
            var link = group["link"];

            if (link.GetProperty("collection")!.GetString()! != "groups")
                throw new Exception($"The provided object is not a group.");

            _id = link.GetProperty("id")!.GetString()!;
        }

        DomainName = domainName;
        Client = client;
    }

    public string DomainName { get; }

    public HsdsClient Client { get; }

    internal IHsdsConnector Connector
    {
        get
        {
            if (_connector is null)
                return (IHsdsConnector)this;

            else
                return _connector;
        }
    }

    /// <inheritdoc />
    public IH5Group Group(string path, H5LinkAccess linkAccess = default)
    {
        if (path == "/")
            return Connector;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ?  (IH5Group)Connector : this;

        for (int i = 0; i < segments.Length; i++)
        {
            try
            {
                var link = Client.Link
                    .GetLinkAsync(id: _id, linkname: segments[i], domain: DomainName)
                    .GetAwaiter()
                    .GetResult();

                current = new H5Group(DomainName, Client, link);                
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