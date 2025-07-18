namespace Bit.Icons.Services;

public class ChangePasswordUriService : IChangePasswordUriService
{
    private readonly HttpClient _httpClient;

    public ChangePasswordUriService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ChangePasswordUri");
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

            var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());

            var response = await _httpClient.SendAsync(request);
            return !response.IsSuccessStatusCode;
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

            var request = new HttpRequestMessage(HttpMethod.Get, url.ToString());

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode ? url.ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}
