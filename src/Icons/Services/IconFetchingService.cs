#nullable enable

using AngleSharp.Html.Parser;
using Bit.Icons.Extensions;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class IconFetchingService : IIconFetchingService
{
    private const int GoogleFaviconSize = 64;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHtmlParser _parser;
    private readonly IUriService _uriService;
    private readonly IconsSettings _iconsSettings;

    public IconFetchingService(
        ILogger<IIconFetchingService> logger,
        IHttpClientFactory httpClientFactory,
        IHtmlParser parser,
        IUriService uriService,
        IconsSettings iconsSettings)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _parser = parser;
        _uriService = uriService;
        _iconsSettings = iconsSettings;
    }

    public async Task<Icon?> GetIconAsync(string domain)
    {
        if (_iconsSettings.GoogleFaviconEnabled)
        {
            var googleIcon = await GetGoogleFaviconAsync(domain);
            if (googleIcon != null)
            {
                return googleIcon;
            }
        }

        var domainIcons = await DomainIcons.FetchAsync(domain, _logger, _httpClientFactory, _parser, _uriService);
        var result = domainIcons.Where(result => result != null).FirstOrDefault();
        return result ?? await GetFaviconAsync(domain);
    }

    private async Task<Icon?> GetGoogleFaviconAsync(string domain)
    {
        // Google's undocumented favicon endpoint. Returns a PNG, 301-redirects to
        // t0.gstatic.com/faviconV2.
        var googleUriBuilder = new UriBuilder
        {
            Scheme = "https",
            Host = "www.google.com",
            Path = "/s2/favicons",
            Query = $"domain={Uri.EscapeDataString(domain)}&sz={GoogleFaviconSize}"
        };

        if (!googleUriBuilder.TryBuild(out var googleUri))
        {
            return null;
        }

        try
        {
            // A 404 from Google indicates no favicon exists for the domain; the existing
            // IconLink/IconHttpRequest pipeline propagates that as a null return.
            return await new IconLink(googleUri!).FetchAsync(_logger, _httpClientFactory, _uriService);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google favicon lookup failed for {domain}.", domain);
            return null;
        }
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
            return await new IconLink(faviconUri!).FetchAsync(_logger, _httpClientFactory, _uriService);
        }
        return null;
    }
}
