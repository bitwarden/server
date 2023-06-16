#nullable enable

using Bit.Icons.Extensions;
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
        var domainIcons = await DomainIcons.Fetch(domain, _logger, _httpClientFactory);
        var result = domainIcons.Where(result => result != null).FirstOrDefault();
        return result ?? await GetFaviconAsync(domain);
    }

    private async Task<Icon?> GetFaviconAsync(string domain)
    {
        // Fall back to favicon
        var faviconUriBuilder = new UriBuilder
        {
            Scheme = "https",
            Host = domain,
            Path = "/favicon.ico"
        };

        if (faviconUriBuilder.TryBuild(out var faviconUri))
        {
            return await new IconLink(faviconUri!).FetchAsync(_logger, _httpClientFactory);
        }
        return null;
    }
}
