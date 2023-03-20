using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class HsdsGroup : HsdsAttributableObject, IH5Group
{
    // this constructor is only for the derived InternalHsdsConnector class
    public HsdsGroup(string name, string id) 
        : base(name, id)
    {
        //
    }

    public HsdsGroup(string name, string id, InternalHsdsConnector connector)
        : base(name, id, connector)
    {
        //
    }

    // TODO: should LinkExists be a Native only method? Here it is implemented
    // using try/catch which makes it quite useless.
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
            .GetResult();
    }

    public Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return InternalGetAsync(path, useAsync: true, cancellationToken);
    }

    // TODO: H5ObjectReference is probably a native only datatype
    public IH5Object Get(H5ObjectReference reference) => throw new NotImplementedException();

    public Task<IH5Object> GetAsync(H5ObjectReference reference, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public IEnumerable<IH5Object> Children()
    {
        return EnumerateReferencesAsync(useAsync: false, default)
            .GetAwaiter()
            .GetResult();
    }

    public Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        return EnumerateReferencesAsync(useAsync: true, cancellationToken);
    }

    private async Task<bool> InternaLinkExists(string path, bool useAsync, CancellationToken cancellationToken)
    {
        try
        {
            await InternalGetAsync(path, useAsync, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IEnumerable<IH5Object>> EnumerateReferencesAsync(bool useAsync, CancellationToken cancellationToken)
    {
        var response = useAsync
            ? await Connector.Client.Link.GetLinksAsync(Id, Connector.DomainName, cancellationToken: cancellationToken).ConfigureAwait(false)
            : Connector.Client.Link.GetLinks(Id, Connector.DomainName);

        return response.Links.Select(link =>
        {
            return (IH5Object)(link.Collection switch
            {
                "groups" => new HsdsGroup(link.Title, link.Id, Connector),
                "datasets" => new HsdsDataset(link.Title, link.Id),
                // https://github.com/HDFGroup/hdf-rest-api/blob/e6f1a685c34ce4db68cdbdbcacacd053176a0136/openapi.yaml#L804-L805
                _ => throw new Exception($"The link collection type {link.Collection} is not supported. Please contact the library maintainer to enable support for this type of collection.")
            });
        });
    }

    private async Task<IH5Object> InternalGetAsync(string path, bool useAsync, CancellationToken cancellationToken)
    {
        if (path == "/")
            return Connector;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        HsdsObject current = isRooted ? Connector : this;

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

                    return linkResponse.Link.Collection switch
                    {
                        "groups" => new HsdsGroup(linkResponse.Link.Title, linkResponse.Link.Id, Connector),
                        "datasets" => new HsdsDataset(linkResponse.Link.Title, linkResponse.Link.Id),
                        // https://github.com/HDFGroup/hdf-rest-api/blob/e6f1a685c34ce4db68cdbdbcacacd053176a0136/openapi.yaml#L804-L805
                        _ => throw new Exception($"The link collection type {linkResponse.Link.Collection} is not supported. Please contact the library maintainer to enable support for this type of collection.")
                    };
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