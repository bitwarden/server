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
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUriService _uriService;
    private readonly int _redirectsCount;
    private readonly Uri _uri;
    private static HttpResponseMessage NotFound => new(HttpStatusCode.NotFound);

    private IconHttpRequest(Uri uri, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, IUriService uriService, int redirectsCount)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient("Icons");
        _uriService = uriService;
        _redirectsCount = redirectsCount;
        _uri = uri;
    }

    public static async Task<IconHttpResponse> FetchAsync(Uri uri, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, IUriService uriService)
    {
        var pageIcons = new IconHttpRequest(uri, logger, httpClientFactory, uriService, 0);
        var httpResponse = await pageIcons.FetchAsync();
        return new IconHttpResponse(httpResponse, logger, httpClientFactory, uriService);
    }

    private async Task<HttpResponseMessage> FetchAsync()
    {
        if (!_uriService.TryGetUri(_uri, out var iconUri) || !iconUri!.IsValid)
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

        try
        {
            return await _httpClient.SendAsync(message);
        }
        catch
        {
            return NotFound;
        }
    }

    private async Task<HttpResponseMessage> FollowRedirectsAsync(HttpResponseMessage response, IconUri originalIconUri)
    {
        if (_redirectsCount >= _maxRedirects || response.Headers.Location == null ||
            !_redirectStatusCodes.Contains(response.StatusCode))
        {
            return NotFound;
        }

        using var responseForRedirect = response;
        var redirectUri = DetermineRedirectUri(responseForRedirect.Headers.Location, originalIconUri);

        return await new IconHttpRequest(redirectUri, _logger, _httpClientFactory, _uriService, _redirectsCount + 1).FetchAsync();
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
