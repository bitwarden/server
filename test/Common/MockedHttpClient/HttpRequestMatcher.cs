#nullable enable

using System.Net;

namespace Bit.Test.Common.MockedHttpClient;

public class HttpRequestMatcher : IHttpRequestMatcher
{
    private readonly Func<HttpRequestMessage, bool> _matcher;
    private HttpRequestMatcher? _childMatcher;
    private MockedHttpResponse _mockedResponse = new(HttpStatusCode.OK);
    private bool _responseSpecified = false;

    public int NumberOfMatches { get; private set; }

    /// <summary>
    /// Returns whether or not the provided request can be handled by this matcher chain.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public bool Matches(HttpRequestMessage request) => _matcher(request) && (_childMatcher == null || _childMatcher.Matches(request));

    public HttpRequestMatcher(HttpMethod method)
    {
        _matcher = request => request.Method == method;
    }

    public HttpRequestMatcher(string uri)
    {
        _matcher = request => request.RequestUri == new Uri(uri);
    }

    public HttpRequestMatcher(Uri uri)
    {
        _matcher = request => request.RequestUri == uri;
    }

    public HttpRequestMatcher(HttpMethod method, string uri)
    {
        _matcher = request => request.Method == method && request.RequestUri == new Uri(uri);
    }

    public HttpRequestMatcher(Func<HttpRequestMessage, bool> matcher)
    {
        _matcher = matcher;
    }

    public HttpRequestMatcher WithHeader(string name, string value)
    {
        return AddChild(request => request.Headers.TryGetValues(name, out var values) && values.Contains(value));
    }

    public HttpRequestMatcher WithQueryParameters(Dictionary<string, string> requiredQueryParameters) =>
        WithQueryParameters(requiredQueryParameters.Select(x => $"{x.Key}={x.Value}").ToArray());
    public HttpRequestMatcher WithQueryParameters(string name, string value) =>
        WithQueryParameters($"{name}={value}");
    public HttpRequestMatcher WithQueryParameters(params string[] queryKeyValues)
    {
        bool matcher(HttpRequestMessage request)
        {
            var query = request.RequestUri?.Query;
            if (query == null)
            {
                return false;
            }

            return queryKeyValues.All(queryKeyValue => query.Contains(queryKeyValue));
        }
        return AddChild(matcher);
    }

    /// <summary>
    /// Configure how this matcher should respond to matching HttpRequestMessages.
    /// Note, after specifying a response, you can no longer further specify match criteria.
    /// </summary>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    public MockedHttpResponse RespondWith(HttpStatusCode statusCode)
    {
        _responseSpecified = true;
        _mockedResponse = new MockedHttpResponse(statusCode);
        return _mockedResponse;
    }

    /// <summary>
    /// Called to produce an HttpResponseMessage for the given request. This is probably something you want to leave alone
    /// </summary>
    /// <param name="request"></param>
    public Task<HttpResponseMessage> RespondToAsync(HttpRequestMessage request)
    {
        NumberOfMatches++;
        return _childMatcher == null ? _mockedResponse.RespondToAsync(request) : _childMatcher.RespondToAsync(request);
    }

    private HttpRequestMatcher AddChild(Func<HttpRequestMessage, bool> matcher)
    {
        if (_responseSpecified)
        {
            throw new Exception("Cannot continue to configure a matcher after a response has been specified");
        }
        _childMatcher = new HttpRequestMatcher(matcher);
        return _childMatcher;
    }
}
