using System.Net;

namespace Bit.Icons.Services;

public class ChangePasswordUriService : IChangePasswordUriService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChangePasswordUriService> _logger;

    public ChangePasswordUriService(IHttpClientFactory httpClientFactory, ILogger<ChangePasswordUriService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ChangePasswordUri");
        _logger = logger;
    }

    /// <summary>
    /// Fetches the well-known change password URL for the given domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <returns>
    /// A <see cref="ChangePasswordUriResult"/> describing whether a URL was found, whether the
    /// domain confirmably does not support it, or whether the lookup failed transiently.
    /// </returns>
    public async Task<ChangePasswordUriResult> GetChangePasswordUri(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return ChangePasswordUriResult.NotSupported;
        }

        try
        {
            var hasReliableStatusCode = await HasReliableHttpStatusCode(domain);
            if (!hasReliableStatusCode)
            {
                // Status codes are unreliable, so a 200 on the well-known URL would be meaningless:
                // a definitive "not supported". Skip the second probe.
                return ChangePasswordUriResult.NotSupported;
            }

            var wellKnownChangePasswordUrl = await GetWellKnownChangePasswordUrl(domain);
            return wellKnownChangePasswordUrl != null
                ? ChangePasswordUriResult.Found(wellKnownChangePasswordUrl)
                : ChangePasswordUriResult.NotSupported;
        }
        catch (Exception ex)
        {
            // Transient probe failure (DNS, timeout, connection reset, SSRF block): not a definitive
            // answer, so surface it rather than caching it as "not supported".
            _logger.LogWarning(ex, "Change-password lookup failed for {Domain}.", domain);
            return ChangePasswordUriResult.LookupFailed;
        }
    }

    /// <summary>
    /// Checks if the server returns a non-200 status code for a resource that should not exist.
    ///  See https://w3c.github.io/webappsec-change-password-url/response-code-reliability.html#semantics
    /// </summary>
    /// <param name="urlDomain">The domain of the URL to check</param>
    /// <returns>True when the domain responds with a non-ok response</returns>
    private async Task<bool> HasReliableHttpStatusCode(string urlDomain)
    {
        // The controller passes a bare host, so force https here — a bare host would otherwise
        // default to http on port 80, probing (and returning) over a plaintext channel.
        var url = new UriBuilder(urlDomain)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            Path = "/.well-known/resource-that-should-not-exist-whose-status-code-should-not-be-200"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        ThrowIfTransient(response.StatusCode);
        return !response.IsSuccessStatusCode;
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
        // Transient failures are not caught here; they propagate to GetChangePasswordUri
        // to be distinguished from a definitive answer. Force https so the probe (and the URL
        // returned to clients) stays on a secure channel; a bare host would default to http:80.
        var url = new UriBuilder(urlDomain)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            Path = "/.well-known/change-password"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        ThrowIfTransient(response.StatusCode);
        return response.IsSuccessStatusCode ? url.ToString() : null;
    }

    /// <summary>
    /// Throws on a transient upstream status (5xx, 429, 408) so it surfaces as a failure instead of
    /// being cached as a definitive "not supported".
    /// </summary>
    private static void ThrowIfTransient(HttpStatusCode status)
    {
        if ((int)status >= 500 ||
            status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout)
        {
            throw new HttpRequestException($"Transient upstream status {(int)status}.", null, status);
        }
    }
}
