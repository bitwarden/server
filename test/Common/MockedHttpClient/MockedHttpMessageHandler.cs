#nullable enable

using System.Net;

namespace Bit.Test.Common.MockedHttpClient;

public class MockedHttpMessageHandler : HttpMessageHandler
{
    private readonly List<IHttpRequestMatcher> _matchers = new();

    /// <summary>
    /// The fallback handler to use when the request does not match any of the provided matchers.
    /// </summary>
    /// <returns>A Matcher that responds with 404 Not Found</returns>
    public MockedHttpResponse Fallback { get; set; } = new(HttpStatusCode.NotFound);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var matcher = _matchers.FirstOrDefault(x => x.Matches(request));
        if (matcher == null)
        {
            return await Fallback.RespondToAsync(request);
        }

        return await matcher.RespondToAsync(request);
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T When<T>(T requestMatcher)
        where T : IHttpRequestMatcher
    {
        _matchers.Add(requestMatcher);
        return requestMatcher;
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public HttpRequestMatcher When(string uri)
    {
        var matcher = new HttpRequestMatcher(uri);
        _matchers.Add(matcher);
        return matcher;
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public HttpRequestMatcher When(Uri uri)
    {
        var matcher = new HttpRequestMatcher(uri);
        _matchers.Add(matcher);
        return matcher;
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public HttpRequestMatcher When(HttpMethod method)
    {
        var matcher = new HttpRequestMatcher(method);
        _matchers.Add(matcher);
        return matcher;
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public HttpRequestMatcher When(HttpMethod method, string uri)
    {
        var matcher = new HttpRequestMatcher(method, uri);
        _matchers.Add(matcher);
        return matcher;
    }

    /// <summary>
    /// Instantiates a new HttpRequestMessage matcher that will handle requests in fitting with the returned matcher. Configuration can be chained.
    /// </summary>
    /// <param name="requestMatcher"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public HttpRequestMatcher When(Func<HttpRequestMessage, bool> matcher)
    {
        var requestMatcher = new HttpRequestMatcher(matcher);
        _matchers.Add(requestMatcher);
        return requestMatcher;
    }

    /// <summary>
    /// Converts the MockedHttpMessageHandler to a HttpClient that can be used in your tests after setup.
    /// </summary>
    /// <returns></returns>
    public HttpClient ToHttpClient()
    {
        return new HttpClient(this);
    }
}
