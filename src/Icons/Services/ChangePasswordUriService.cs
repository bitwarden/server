using System.Net;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class ChangePasswordUriService : IChangePasswordUriService
{
    private const int _maxRedirects = 2;

    private static readonly HttpStatusCode[] _redirectStatusCodes =
    [
        HttpStatusCode.Redirect,
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.RedirectKeepVerb,
        HttpStatusCode.SeeOther
    ];

    private readonly HttpClient _httpClient;
    private readonly IUriService _uriService;

    public ChangePasswordUriService(IHttpClientFactory httpClientFactory, IUriService uriService)
    {
        _httpClient = httpClientFactory.CreateClient("ChangePasswordUri");
        _uriService = uriService;
    }

    /// <summary>
    /// Fetches the well-known change password URL for the given domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    public async Task<string?> GetChangePasswordUri(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var hasReliableStatusCode = await HasReliableHttpStatusCode(domain);
        var wellKnownChangePasswordUrl = await GetWellKnownChangePasswordUrl(domain);

        if (hasReliableStatusCode && wellKnownChangePasswordUrl != null)
        {
            return wellKnownChangePasswordUrl;
        }

        // Reliable well-known URL criteria not met, return null
        return null;
    }

    /// <summary>
    /// Checks if the server returns a non-200 status code for a resource that should not exist.
    //  See https://w3c.github.io/webappsec-change-password-url/response-code-reliability.html#semantics
    /// </summary>
    /// <param name="urlDomain">The domain of the URL to check</param>
    /// <returns>True when the domain responds with a non-ok response</returns>
    private async Task<bool> HasReliableHttpStatusCode(string urlDomain)
    {
        try
        {
            var url = new UriBuilder(urlDomain)
            {
                Path = "/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200"
            };

            var response = await SendSafeRequestAsync(url.Uri);
            if (response == null)
            {
                return false;
            }

            using (response)
            {
                return !response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds a well-known change password URL for the given origin. Attempts to fetch the URL to ensure a valid response
    /// is returned. Returns null if the request throws or the response is not 200 OK.
    /// See https://w3c.github.io/webappsec-change-password-url/
    /// </summary>
    /// <param name="urlDomain">The domain of the URL to check</param>
    /// <returns>The well-known change password URL if valid, otherwise null</returns>
    private async Task<string?> GetWellKnownChangePasswordUrl(string urlDomain)
    {
        try
        {
            var url = new UriBuilder(urlDomain)
            {
                Path = "/.well-known/change-password"
            };

            var response = await SendSafeRequestAsync(url.Uri);
            if (response == null)
            {
                return null;
            }

            using (response)
            {
                return response.IsSuccessStatusCode ? url.ToString() : null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sends an HTTP GET request with SSRF protections: validates the target IP is not internal,
    /// binds the request to the resolved IP to prevent DNS rebinding, and manually follows redirects
    /// with validation at each hop.
    /// </summary>
    /// <returns>The HTTP response, or null if the URI fails SSRF validation.</returns>
    private async Task<HttpResponseMessage?> SendSafeRequestAsync(Uri uri)
    {
        if (!_uriService.TryGetUri(uri, out var iconUri) || !iconUri!.IsValid)
        {
            return null;
        }

        return await SendWithRedirectsAsync(iconUri, 0);
    }

    private async Task<HttpResponseMessage?> SendWithRedirectsAsync(IconUri iconUri, int redirectCount)
    {
        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, iconUri.InnerUri);
            message.Headers.Host = iconUri.Host;
            response = await _httpClient.SendAsync(message);
        }
        catch
        {
            return null;
        }

        if (response.IsSuccessStatusCode || !_redirectStatusCodes.Contains(response.StatusCode))
        {
            return response;
        }

        // Handle redirect with SSRF validation
        using (response)
        {
            if (redirectCount >= _maxRedirects || response.Headers.Location == null)
            {
                return null;
            }

            if (!_uriService.TryGetRedirect(response, iconUri, out var redirectIconUri) || !redirectIconUri!.IsValid)
            {
                return null;
            }

            return await SendWithRedirectsAsync(redirectIconUri, redirectCount + 1);
        }
    }
}
