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

namespace {{0}};

/// <summary>
/// A client for the HSDS system.
/// </summary>
public interface I{{1}}
{
{{10}}
}

/// <inheritdoc />
public class {{1}} : I{{1}}, IDisposable
{
    private HttpClient _httpClient;

{{4}}
    /// <summary>
    /// Initializes a new instance of the <see cref="{{1}}"/>.
    /// </summary>
    /// <param name="baseUrl">The base URL to connect to.</param>
    public {{1}}(Uri baseUrl) : this(new HttpClient() { BaseAddress = baseUrl })
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="{{1}}"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    public {{1}}(HttpClient httpClient)
    {
        if (httpClient.BaseAddress is null)
            throw new Exception("The base address of the HTTP client must be set.");

        _httpClient = httpClient;

{{5}}
    }

{{6}}

    internal async Task<T> InvokeAsync<T>(string method, string relativeUrl, string? acceptHeaderValue, string? contentTypeValue, HttpContent? content, CancellationToken cancellationToken)
    {
        // prepare request
        using var request = BuildRequestMessage(method, relativeUrl, content, contentTypeValue, acceptHeaderValue);

        // send request
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            var statusCode = $"H00.{(int)response.StatusCode}";

            if (string.IsNullOrWhiteSpace(message))
                throw new {{8}}(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

            else
                throw new {{8}}(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
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
                    throw new {{8}}("H01", "Response data could not be deserialized.", ex);
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

{{7}}

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
#if NETSTANDARD2_0 || NETSTANDARD2_1
        return _response.Content.ReadAsStreamAsync();
#else
        return _response.Content.ReadAsStreamAsync(cancellationToken);
#endif
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _response.Dispose();
    }
}

/// <summary>
/// A {{8}}.
/// </summary>
public class {{8}} : Exception
{
    internal {{8}}(string statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    internal {{8}}(string statusCode, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// The exception status code.
    /// </summary>
    public string StatusCode { get; }
}

{{9}}

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