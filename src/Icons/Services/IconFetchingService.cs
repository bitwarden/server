#nullable enable

using AngleSharp.Html.Parser;
using Bit.Icons.Extensions;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class IconFetchingService : IIconFetchingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHtmlParser _parser;

    public IconFetchingService(ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, IHtmlParser parser)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _parser = parser;
    }

    public async Task<Icon?> GetIconAsync(string domain)
    {
        var domainIcons = await DomainIcons.FetchAsync(domain, _logger, _httpClientFactory, _parser);
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
