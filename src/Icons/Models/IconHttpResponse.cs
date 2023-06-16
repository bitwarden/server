#nullable enable

using System.Collections;
using System.Net;
using AngleSharp.Html.Parser;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class IconHttpResponse : IEnumerable<Icon>, IDisposable
{
    private const int _maxIconLinksProcessed = 200;
    private const int _maxRetrievedIcons = 10;

    private readonly HttpResponseMessage _response;
    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly List<Icon> _icons = new();

    public HttpStatusCode StatusCode => _response.StatusCode;
    public bool IsSuccessStatusCode => _response.IsSuccessStatusCode;
    public string? ContentType => _response.Content.Headers.ContentType?.MediaType;
    public HttpContent Content => _response.Content;
    public Icon this[int i]
    {
        get
        {
            return _icons[i];
        }
    }
    public IEnumerator<Icon> GetEnumerator() => ((IEnumerable<Icon>)_icons).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_icons).GetEnumerator();

    public IconHttpResponse(HttpResponseMessage response, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory)
    {
        _response = response;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IEnumerable<Icon>> RetrieveIconsAsync(Uri requestUri, string domain)
    {
        var parser = new HtmlParser();
        using var htmlStream = await _response.Content.ReadAsStreamAsync();
        using var document = parser.ParseDocument(htmlStream);

        if (document.DocumentElement == null)
        {
            _logger.LogWarning("No DocumentElement for {domain}.", domain);
            return Array.Empty<Icon>();
        }

        // Make sure uri uses domain name, not ip
        var uri = _response.RequestMessage?.RequestUri;
        if (uri == null || IPAddress.TryParse(_response.RequestMessage!.RequestUri!.Host, out var _))
        {
            uri = requestUri;
        }

        var baseUrl = document.QuerySelector("head base[href]")?.Attributes["href"]?.Value;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "/";
        }

        var links = document.QuerySelectorAll("head link[href]")
            ?.Take(_maxIconLinksProcessed)
            .Select(l => new IconLink(l, uri, baseUrl))
            .Where(l => l.IsUsable())
            .OrderBy(l => l.Priority)
            .Take(_maxRetrievedIcons)
            .ToList() ?? new List<IconLink>();
        return await Task.WhenAll(links.Select(l => l.FetchAsync(_logger, _httpClientFactory)));
    }


    public void Dispose()
    {
        _response.Dispose();
    }
}
