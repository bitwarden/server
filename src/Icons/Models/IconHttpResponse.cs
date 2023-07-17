#nullable enable

using System.Net;
using AngleSharp.Html.Parser;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class IconHttpResponse : IDisposable
{
    private const int _maxIconLinksProcessed = 200;
    private const int _maxRetrievedIcons = 10;

    private readonly HttpResponseMessage _response;
    private readonly ILogger<IIconFetchingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUriService _uriService;

    public HttpStatusCode StatusCode => _response.StatusCode;
    public bool IsSuccessStatusCode => _response.IsSuccessStatusCode;
    public string? ContentType => _response.Content.Headers.ContentType?.MediaType;
    public HttpContent Content => _response.Content;

    public IconHttpResponse(HttpResponseMessage response, ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, IUriService uriService)
    {
        _response = response;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _uriService = uriService;
    }

    public async Task<IEnumerable<Icon>> RetrieveIconsAsync(Uri requestUri, string domain, IHtmlParser parser)
    {
        using var htmlStream = await _response.Content.ReadAsStreamAsync();
        var head = await parser.ParseHeadAsync(htmlStream);

        if (head == null)
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

        var baseUrl = head.QuerySelector("base[href]")?.Attributes["href"]?.Value;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "/";
        }

        var links = head.QuerySelectorAll("link[href]")
            ?.Take(_maxIconLinksProcessed)
            .Select(l => new IconLink(l, uri, baseUrl))
            .Where(l => l.IsUsable())
            .OrderBy(l => l.Priority)
            .Take(_maxRetrievedIcons)
            .ToArray() ?? Array.Empty<IconLink>();
        var results = await Task.WhenAll(links.Select(l => l.FetchAsync(_logger, _httpClientFactory, _uriService)));
        return results.Where(r => r != null).Select(r => r!);
    }


    public void Dispose()
    {
        _response.Dispose();
    }
}
