using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class HsdsGroup : HsdsAttributableObject, IH5Group
{
    // this constructor is only for the derived InternalHsdsConnector class
    public HsdsGroup(HsdsNamedReference reference) 
        : base(reference)
    {
        //
    }

    public HsdsGroup(InternalHsdsConnector connector, HsdsNamedReference reference)
        : base(connector, reference)
    {
        //
    }

    public bool LinkExists(string path)
    {
        return InternaLinkExists(path, useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<bool> LinkExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return InternaLinkExists(path, useAsync: true, cancellationToken);
    }

    public IH5Object Get(string path)
    {
        return InternalGetAsync(path, useAsync: false, default)
            .GetAwaiter()
            .GetResult()
            .Dereference();
    }

    public async Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return (await InternalGetAsync(path, useAsync: true, cancellationToken))
            .Dereference();
    }

    public IEnumerable<IH5Object> Children()
    {
        return EnumerateReferencesAsync(useAsync: false, default)
            .GetAwaiter()
            .GetResult()
            .Select(reference => reference.Dereference());
    }

    public async Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        return (await EnumerateReferencesAsync(useAsync: true, cancellationToken))
            .Select(reference => reference.Dereference());
    }

    private Task<bool> InternaLinkExists(string path, bool useAsync, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
    }

    private async Task<IEnumerable<HsdsNamedReference>> EnumerateReferencesAsync(bool useAsync, CancellationToken cancellationToken)
    {
        var response = useAsync
            ? await Connector.Client.Link.GetLinksAsync(Id, Connector.DomainName, cancellationToken: cancellationToken).ConfigureAwait(false)
            : Connector.Client.Link.GetLinks(Id, Connector.DomainName);

        return response.Links
            .Select(link => new HsdsNamedReference(link.Collection, link.Title, link.Id, Connector));
    }

    private async Task<HsdsNamedReference> InternalGetAsync(string path, bool useAsync, CancellationToken cancellationToken)
    {
        if (path == "/")
            return Connector.Reference;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Connector.Reference : Reference;

        for (int i = 0; i < segments.Length; i++)
        {
            try
            {
                var key = new CacheEntryKey(current.Id, segments[i]);

                current = await Connector.Cache.GetOrAddAsync(key, async () =>
                {
                    GetLinkResponse linkResponse;

                    if (useAsync)
                    {
                        linkResponse = await Connector.Client.Link
                            .GetLinkAsync(id: key.ParentId, linkname: key.LinkName, domain: Connector.DomainName, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        linkResponse = Connector.Client.Link
                            .GetLink(id: key.ParentId, linkname: key.LinkName, domain: Connector.DomainName);
                    }

                    var link = linkResponse.Link;
                    return new HsdsNamedReference(link.Collection, link.Title, link.Id, Connector);
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