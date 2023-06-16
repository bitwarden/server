#nullable enable

using System.Net;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class IconFetchingService : IIconFetchingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IIconFetchingService> _logger;

    public IconFetchingService(ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Icon?> GetIconAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || IPAddress.TryParse(domain, out _))
        {
            return null;
        }

        var domainIcons = await DomainIcons.Fetch(domain, _logger, _httpClientFactory);
        var result = domainIcons.Where(result => result != null).FirstOrDefault();
        return result ?? await GetFaviconAsync(domain);
    }

    private async Task<Icon?> GetFaviconAsync(string domain)
    {
        // Fall back to favicon
        var faviconUri = new UriBuilder
        {
            Scheme = "https",
            Host = domain,
            Path = "/favicon.ico"
        }.Uri;
        return await new IconLink(faviconUri).FetchAsync(_logger, _httpClientFactory);
    }
}
