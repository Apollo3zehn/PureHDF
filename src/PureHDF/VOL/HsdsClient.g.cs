#nullable enable

// 0 = Namespace
// 1 = ClientName
// 2 = -
// 3 = -
// 4 = SubClientFields
// 5 = SubClientFieldAssignment
// 6 = SubClientProperties
// 7 = SubClientSource
// 8 = ExceptionType
// 9 = Models
// 10 = SubClientInterfaceProperties

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PureHDF;

/// <summary>
/// A client for the HSDS system.
/// </summary>
public interface IHsdsClient
{
    /// <summary>
    /// Gets the <see cref="IDomainClient"/>.
    /// </summary>
    IDomainClient Domain { get; }

    /// <summary>
    /// Gets the <see cref="IGroupClient"/>.
    /// </summary>
    IGroupClient Group { get; }

    /// <summary>
    /// Gets the <see cref="ILinkClient"/>.
    /// </summary>
    ILinkClient Link { get; }

    /// <summary>
    /// Gets the <see cref="IDatasetClient"/>.
    /// </summary>
    IDatasetClient Dataset { get; }

    /// <summary>
    /// Gets the <see cref="IDatatypeClient"/>.
    /// </summary>
    IDatatypeClient Datatype { get; }

    /// <summary>
    /// Gets the <see cref="IAttributeClient"/>.
    /// </summary>
    IAttributeClient Attribute { get; }

    /// <summary>
    /// Gets the <see cref="IACLSClient"/>.
    /// </summary>
    IACLSClient ACLS { get; }


}

/// <inheritdoc />
public class HsdsClient : IHsdsClient, IDisposable
{
    private HttpClient _httpClient;

    private DomainClient _domain;
    private GroupClient _group;
    private LinkClient _link;
    private DatasetClient _dataset;
    private DatatypeClient _datatype;
    private AttributeClient _attribute;
    private ACLSClient _aCLS;

    /// <summary>
    /// Initializes a new instance of the <see cref="HsdsClient"/>.
    /// </summary>
    /// <param name="baseUrl">The base URL to connect to.</param>
    public HsdsClient(Uri baseUrl) : this(new HttpClient() { BaseAddress = baseUrl })
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HsdsClient"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    public HsdsClient(HttpClient httpClient)
    {
        if (httpClient.BaseAddress is null)
            throw new Exception("The base address of the HTTP client must be set.");

        _httpClient = httpClient;

        _domain = new DomainClient(this);
        _group = new GroupClient(this);
        _link = new LinkClient(this);
        _dataset = new DatasetClient(this);
        _datatype = new DatatypeClient(this);
        _attribute = new AttributeClient(this);
        _aCLS = new ACLSClient(this);

    }

    /// <inheritdoc />
    public IDomainClient Domain => _domain;

    /// <inheritdoc />
    public IGroupClient Group => _group;

    /// <inheritdoc />
    public ILinkClient Link => _link;

    /// <inheritdoc />
    public IDatasetClient Dataset => _dataset;

    /// <inheritdoc />
    public IDatatypeClient Datatype => _datatype;

    /// <inheritdoc />
    public IAttributeClient Attribute => _attribute;

    /// <inheritdoc />
    public IACLSClient ACLS => _aCLS;



    internal async Task<T> InvokeAsync<T>(string method, string relativeUrl, string? acceptHeaderValue, string? contentTypeValue, HttpContent? content, CancellationToken cancellationToken)
    {
        // prepare request
        using var request = BuildRequestMessage(method, relativeUrl, content, contentTypeValue, acceptHeaderValue);

        // send request
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            var statusCode = $"V00.{(int)response.StatusCode}";

            if (string.IsNullOrWhiteSpace(message))
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

            else
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
        }

        try
        {
            if (typeof(T) == typeof(object))
            {
                return default!;
            }

            else if (typeof(T) == typeof(StreamResponse))
            {
                return (T)(object)(new StreamResponse(response));
            }

            else
            {
#if NETSTANDARD2_0 || NETSTANDARD2_1
                var stream = await response.Content.ReadAsStreamAsync();
#else
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#endif

                try
                {
                    return (await JsonSerializer.DeserializeAsync<T>(stream, Utilities.JsonOptions))!;
                }
                catch (Exception ex)
                {
                    throw new HsdsException("V01", "Response data could not be deserialized.", ex);
                }
            }
        }
        finally
        {
            if (typeof(T) != typeof(StreamResponse))
                response.Dispose();
        }
    }
    
    private HttpRequestMessage BuildRequestMessage(string method, string relativeUrl, HttpContent? content, string? contentTypeHeaderValue, string? acceptHeaderValue)
    {
        var requestMessage = new HttpRequestMessage()
        {
            Method = new HttpMethod(method),
            RequestUri = new Uri(relativeUrl, UriKind.Relative),
            Content = content
        };

        if (contentTypeHeaderValue is not null && requestMessage.Content is not null)
            requestMessage.Content.Headers.ContentType = MediaTypeWithQualityHeaderValue.Parse(contentTypeHeaderValue);

        if (acceptHeaderValue is not null)
            requestMessage.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeaderValue));

        return requestMessage;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Provides methods to interact with domain.
/// </summary>
public interface IDomainClient
{
    /// <summary>
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutDomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> DeleteDomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DomainClient : IDomainClient
{
    private HsdsClient _client;
    
    internal DomainClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutDomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (folder is not null) queryValues.Add("folder", Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> DeleteDomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("DELETE", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls/{user}");
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls/{user}");
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with group.
/// </summary>
public interface IGroupClient
{
    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="getalias"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> DeleteGroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class GroupClient : IGroupClient
{
    private HsdsClient _client;
    
    internal GroupClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (getalias is not null) queryValues.Add("getalias", Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> DeleteGroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("DELETE", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));
        if (Marker is not null) queryValues.Add("Marker", Uri.EscapeDataString(Convert.ToString(Marker, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/acls/{user}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with link.
/// </summary>
public interface ILinkClient
{
    /// <summary>
    /// List all Links in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Links returned in list.</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetLinkAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Link to create.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutLinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> DeleteLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class LinkClient : ILinkClient
{
    private HsdsClient _client;
    
    internal LinkClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetLinkAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/links");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));
        if (Marker is not null) queryValues.Add("Marker", Uri.EscapeDataString(Convert.ToString(Marker, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutLinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/links/{linkname}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{linkname}", Uri.EscapeDataString(Convert.ToString(linkname, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/links/{linkname}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{linkname}", Uri.EscapeDataString(Convert.ToString(linkname, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> DeleteLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/links/{linkname}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{linkname}", Uri.EscapeDataString(Convert.ToString(linkname, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("DELETE", url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with dataset.
/// </summary>
public interface IDatasetClient
{
    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> DeleteDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">Array of nonzero integers.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing what to write.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task PutValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<StreamResponse> GetValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DatasetClient : IDatasetClient
{
    private HsdsClient _client;
    
    internal DatasetClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> DeleteDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("DELETE", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/shape");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/shape");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/type");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task PutValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/value");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<object>("PUT", url, default, "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<StreamResponse> GetValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/value");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (select is not null) queryValues.Add("select", Uri.EscapeDataString(Convert.ToString(select, CultureInfo.InvariantCulture)!));
        if (query is not null) queryValues.Add("query", Uri.EscapeDataString(Convert.ToString(query, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<StreamResponse>("GET", url, "application/octet-stream", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/value");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));
        if (Marker is not null) queryValues.Add("Marker", Uri.EscapeDataString(Convert.ToString(Marker, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with datatype.
/// </summary>
public interface IDatatypeClient
{
    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> DeleteDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DatatypeClient : IDatatypeClient
{
    private HsdsClient _client;
    
    internal DatatypeClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("POST", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> DeleteDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes/{id}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("DELETE", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));
        if (Marker is not null) queryValues.Add("Marker", Uri.EscapeDataString(Convert.ToString(Marker, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with attribute.
/// </summary>
public interface IAttributeClient
{
    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class AttributeClient : IAttributeClient
{
    private HsdsClient _client;
    
    internal AttributeClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));
        if (Limit is not null) queryValues.Add("Limit", Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!));
        if (Marker is not null) queryValues.Add("Marker", Uri.EscapeDataString(Convert.ToString(Marker, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        urlBuilder.Replace("{collection}", Uri.EscapeDataString(Convert.ToString(collection, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(Convert.ToString(obj_uuid, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{attr}", Uri.EscapeDataString(Convert.ToString(attr, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with acls.
/// </summary>
public interface IACLSClient
{
    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class ACLSClient : IACLSClient
{
    private HsdsClient _client;
    
    internal ACLSClient(HsdsClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls");

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls/{user}");
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/acls/{user}");
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("PUT", url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/groups/{id}/acls/{user}");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));
        urlBuilder.Replace("{user}", Uri.EscapeDataString(Convert.ToString(user, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datasets/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, JsonElement>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append("/datatypes/{id}/acls");
        urlBuilder.Replace("{id}", Uri.EscapeDataString(Convert.ToString(id, CultureInfo.InvariantCulture)!));

        var queryValues = new Dictionary<string, string>();
        if (domain is not null) queryValues.Add("domain", Uri.EscapeDataString(Convert.ToString(domain, CultureInfo.InvariantCulture)!));

        var __query = queryValues.Any() ? "?" + string.Join("&", queryValues.Select(entry => $"{entry.Key}={entry.Value}")) : default;
        urlBuilder.Append(__query);

        var url = urlBuilder.ToString();
        return _client.InvokeAsync<IReadOnlyDictionary<string, JsonElement>>("GET", url, "application/json", default, default, cancellationToken);
    }

}



/// <summary>
/// A stream response. 
/// </summary>
public class StreamResponse : IDisposable
{
    private long _length;
    private HttpResponseMessage _response;

    internal StreamResponse(HttpResponseMessage response)
    {
        _response = response;
       
        if (_response.Content.Headers.TryGetValues("Content-Length", out var values) && 
            values.Any() && 
            int.TryParse(values.First(), out var contentLength))
        {
            _length = contentLength;
        }
        else
        {
            _length = -1;
        }
    }

    /// <summary>
    /// Returns the underlying stream.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
    {
        return _response.Content.ReadAsStreamAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _response.Dispose();
    }
}

/// <summary>
/// A HsdsException.
/// </summary>
public class HsdsException : Exception
{
    internal HsdsException(string statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    internal HsdsException(string statusCode, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// The exception status code.
    /// </summary>
    public string StatusCode { get; }
}

/// <summary>
/// Access Control List for a single user.
/// </summary>
/// <param name="Username"></param>
public record ACL(IReadOnlyDictionary<string, JsonElement> Username);

/// <summary>
/// Access Control Lists for users.
/// </summary>
/// <param name="ForWhom">Access Control List for a single user.</param>
public record ACLS(ACL ForWhom);



internal static class Utilities
{
    internal static JsonSerializerOptions JsonOptions { get; }

    static Utilities()
    {
        JsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }
}