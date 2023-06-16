#nullable enable

using System.Collections;
using Bit.Icons.Extensions;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class DomainIcons : IEnumerable<Icon>
{
    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly List<Icon> _icons = new();

    public string Domain { get; }
    public Icon this[int i]
    {
        get
        {
            return _icons[i];
        }
    }
    public IEnumerator<Icon> GetEnumerator() => ((IEnumerable<Icon>)_icons).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_icons).GetEnumerator();

    private DomainIcons(string domain, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        Domain = domain;
    }

    public static async Task<DomainIcons> Fetch(string domain, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory)
    {
        var pageIcons = new DomainIcons(domain, logger, httpClientFactory);
        await pageIcons.FetchIconsAsync();
        return pageIcons;
    }


    private async Task FetchIconsAsync()
    {
        if (!Uri.TryCreate($"https://{Domain}", UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Bad domain: {domain}.", Domain);
            return;
        }

        var host = uri.Host;

        // first try https
        using (var response = await IconHttpRequest.FetchAsync(uri, _logger, _httpClientFactory))
        {
            if (response.IsSuccessStatusCode)
            {
                _icons.AddRange(await response.RetrieveIconsAsync(uri, Domain));
                return;
            }
        }

        // then try http
        uri = uri.ChangeScheme("http");
        using (var response = await IconHttpRequest.FetchAsync(uri, _logger, _httpClientFactory))
        {
            if (response.IsSuccessStatusCode)
            {
                _icons.AddRange(await response.RetrieveIconsAsync(uri, Domain));
                return;
            }
        }

        var dotCount = Domain.Count(c => c == '.');

        // Then try base domain
        if (dotCount > 1 && DomainName.TryParseBaseDomain(Domain, out var baseDomain) &&
            Uri.TryCreate($"https://{baseDomain}", UriKind.Absolute, out uri))
        {
            using var response = await IconHttpRequest.FetchAsync(uri, _logger, _httpClientFactory);
            if (response.IsSuccessStatusCode)
            {
                _icons.AddRange(await response.RetrieveIconsAsync(uri, Domain));
                return;
            }
        }

        // Then try www
        if (dotCount < 2 && Uri.TryCreate($"https://www.{host}", UriKind.Absolute, out uri))
        {
            using var response = await IconHttpRequest.FetchAsync(uri, _logger, _httpClientFactory);
            if (response.IsSuccessStatusCode)
            {
                _icons.AddRange(await response.RetrieveIconsAsync(uri, Domain));
                return;
            }
        }
    }
}
