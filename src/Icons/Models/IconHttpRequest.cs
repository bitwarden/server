#nullable enable

using System.Net;
using Bit.Icons.Extensions;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class IconHttpRequest
{
    private const int _maxRedirects = 2;

    private readonly HttpStatusCode[] _redirectStatusCodes = new HttpStatusCode[] { HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently, HttpStatusCode.RedirectKeepVerb, HttpStatusCode.SeeOther };

    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly int _redirectsCount;
    private readonly Uri _uri;
    private static HttpResponseMessage NotFound => new(HttpStatusCode.NotFound);

    private IconHttpRequest(Uri uri, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, int redirectsCount)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _redirectsCount = redirectsCount;
        _uri = uri;
    }

    public static async Task<IconHttpResponse> FetchAsync(Uri uri, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory)
    {
        var pageIcons = new IconHttpRequest(uri, logger, httpClientFactory, 0);
        var httpResponse = await pageIcons.FetchAsync();
        return new IconHttpResponse(httpResponse, logger, httpClientFactory); ;
    }

    private async Task<HttpResponseMessage> FetchAsync()
    {
        if (!IconUri.TryCreate(_uri, out var iconUri) || !iconUri!.IsValid)
        {
            return NotFound;
        }

        var response = await GetAsync(iconUri);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using var responseForRedirect = response;
        return await FollowRedirectsAsync(responseForRedirect, iconUri);
    }


    private async Task<HttpResponseMessage> GetAsync(IconUri iconUri)
    {
        using var message = new HttpRequestMessage();
        message.RequestUri = iconUri.InnerUri;
        message.Headers.Host = iconUri.Host;
        message.Method = HttpMethod.Get;

        // Let's add some headers to look like we're coming from a web browser request. Some websites
        // will block our request without these.
        message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16299");
        message.Headers.Add("Accept-Language", "en-US,en;q=0.8");
        message.Headers.Add("Cache-Control", "no-cache");
        message.Headers.Add("Pragma", "no-cache");
        message.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;" +
            "q=0.9,image/webp,image/apng,*/*;q=0.8");

        try
        {
            return await _httpClientFactory.CreateClient("Icons").SendAsync(message);
        }
        catch
        {
            return NotFound;
        }
    }

    private async Task<HttpResponseMessage> FollowRedirectsAsync(HttpResponseMessage response, IconUri originalIconUri)
    {
        if (_redirectsCount > _maxRedirects || response.Headers.Location == null ||
            !_redirectStatusCodes.Contains(response.StatusCode))
        {
            return NotFound;
        }

        using var responseForRedirect = response;
        var redirectUri = DetermineRedirectUri(responseForRedirect.Headers.Location, originalIconUri);

        if (!IconUri.TryCreate(redirectUri, out var redirectIconUri) || !redirectIconUri!.IsValid)
        {
            return NotFound;
        }

        return await new IconHttpRequest(redirectUri, _logger, _httpClientFactory, _redirectsCount + 1).FetchAsync();
    }

    private static Uri DetermineRedirectUri(Uri responseUri, IconUri originalIconUri)
    {
        if (responseUri.IsAbsoluteUri)
        {
            if (!responseUri.IsHypertext())
            {
                return responseUri.ChangeScheme("https");
            }
            return responseUri;
        }
        else
        {
            return new UriBuilder
            {
                Scheme = originalIconUri.Scheme,
                Host = originalIconUri.Host,
                Path = responseUri.ToString()
            }.Uri;
        }
    }
}
