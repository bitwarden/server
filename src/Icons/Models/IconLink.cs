#nullable enable

using System.Text;
using AngleSharp.Dom;
using Bit.Icons.Extensions;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class IconLink
{
    private static readonly HashSet<string> _iconRels = new() { "icon", "apple-touch-icon", "shortcut icon" };
    private static readonly HashSet<string> _blacklistedRels = new() { "preload", "image_src", "preconnect", "canonical", "alternate", "stylesheet" };
    private static readonly HashSet<string> _iconExtensions = new() { ".ico", ".png", ".jpg", ".jpeg" };
    private const string _pngMediaType = "image/png";
    private static readonly byte[] _pngHeader = new byte[] { 137, 80, 78, 71 };
    private static readonly byte[] _webpHeader = Encoding.UTF8.GetBytes("RIFF");

    private const string _icoMediaType = "image/x-icon";
    private const string _icoAltMediaType = "image/vnd.microsoft.icon";
    private static readonly byte[] _icoHeader = new byte[] { 00, 00, 01, 00 };

    private const string _jpegMediaType = "image/jpeg";
    private static readonly byte[] _jpegHeader = new byte[] { 255, 216, 255 };

    private static readonly HashSet<string> _allowedMediaTypes = new()
    {
        _pngMediaType,
        _icoMediaType,
        _icoAltMediaType,
        _jpegMediaType,
    };

    private bool _useUriDirectly = false;
    private bool _validated = false;
    private int? _width;
    private int? _height;

    public IAttr? Href { get; }
    public IAttr? Rel { get; }
    public IAttr? Type { get; }
    public IAttr? Sizes { get; }
    public Uri ParentUri { get; }
    public string BaseUrlPath { get; }
    public int Priority
    {
        get
        {
            if (_width == null || _width != _height)
            {
                return 200;
            }

            return _width switch
            {
                32 => 1,
                64 => 2,
                >= 24 and <= 128 => 3,
                16 => 4,
                _ => 100,
            };
        }
    }

    public IconLink(Uri parentPage)
    {
        _useUriDirectly = true;
        _validated = true;
        ParentUri = parentPage;
        BaseUrlPath = parentPage.PathAndQuery;
    }

    public IconLink(IElement element, Uri parentPage, string baseUrlPath)
    {
        Href = element.Attributes["href"];
        ParentUri = parentPage;
        BaseUrlPath = baseUrlPath;

        Rel = element.Attributes["rel"];
        Type = element.Attributes["type"];
        Sizes = element.Attributes["sizes"];

        if (!string.IsNullOrWhiteSpace(Sizes?.Value))
        {
            var sizeParts = Sizes.Value.Split('x');
            if (sizeParts.Length == 2 && int.TryParse(sizeParts[0].Trim(), out var width) &&
                int.TryParse(sizeParts[1].Trim(), out var height))
            {
                _width = width;
                _height = height;
            }
        }
    }

    public bool IsUsable()
    {
        if (string.IsNullOrWhiteSpace(Href?.Value))
        {
            return false;
        }

        if (Rel != null && _iconRels.Contains(Rel.Value.ToLower()))
        {
            _validated = true;
        }
        if (Rel == null || !_blacklistedRels.Contains(Rel.Value.ToLower()))
        {
            try
            {
                var extension = Path.GetExtension(Href.Value);
                if (_iconExtensions.Contains(extension.ToLower()))
                {
                    _validated = true;
                }
            }
            catch (ArgumentException) { }
        }
        return _validated;
    }

    /// <summary>
    /// Fetches the icon from the Href. Will always fail unless first validated with IsUsable().
    /// </summary>
    public async Task<Icon?> FetchAsync(ILogger<IIconFetchingService> logger, IHttpClientFactory httpClientFactory, IUriService uriService)
    {
        if (!_validated)
        {
            return null;
        }

        var uri = BuildUri();
        if (uri == null)
        {
            return null;
        }

        using var response = await IconHttpRequest.FetchAsync(uri, logger, httpClientFactory, uriService);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var format = response.Content.Headers.ContentType?.MediaType;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (format == null || !_allowedMediaTypes.Contains(format))
        {
            format = DetermineImageFormatFromFile(bytes);
        }

        return new Icon { Image = bytes, Format = format };
    }

    private Uri? BuildUri()
    {
        if (_useUriDirectly)
        {
            return ParentUri;
        }

        if (Href == null)
        {
            return null;
        }

        if (Href.Value.StartsWith("//") && Uri.TryCreate($"{ParentUri.Scheme}://{Href.Value[2..]}", UriKind.Absolute, out var uri))
        {
            return uri;
        }

        if (Uri.TryCreate(Href.Value, UriKind.Relative, out uri))
        {
            return new UriBuilder()
            {
                Scheme = ParentUri.Scheme,
                Host = ParentUri.Host,
            }.Uri.ConcatPath(BaseUrlPath, uri.OriginalString);
        }

        if (Uri.TryCreate(Href.Value, UriKind.Absolute, out uri))
        {
            return uri;
        }

        return null;
    }

    private static bool HeaderMatch(byte[] imageBytes, byte[] header)
    {
        return imageBytes.Length >= header.Length && header.SequenceEqual(imageBytes.Take(header.Length));
    }

    private static string DetermineImageFormatFromFile(byte[] imageBytes)
    {
        if (HeaderMatch(imageBytes, _icoHeader))
        {
            return _icoMediaType;
        }
        else if (HeaderMatch(imageBytes, _pngHeader) || HeaderMatch(imageBytes, _webpHeader))
        {
            return _pngMediaType;
        }
        else if (HeaderMatch(imageBytes, _jpegHeader))
        {
            return _jpegMediaType;
        }
        else
        {
            return string.Empty;
        }
    }
}
