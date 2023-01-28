using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using Bit.Icons.Models;

namespace Bit.Icons.Services;

public class IconFetchingService : IIconFetchingService
{
    private readonly HashSet<string> _iconRels =
        new HashSet<string> { "icon", "apple-touch-icon", "shortcut icon" };
    private readonly HashSet<string> _blacklistedRels =
        new HashSet<string> { "preload", "image_src", "preconnect", "canonical", "alternate", "stylesheet" };
    private readonly HashSet<string> _iconExtensions =
        new HashSet<string> { ".ico", ".png", ".jpg", ".jpeg" };

    private readonly string _pngMediaType = "image/png";
    private readonly byte[] _pngHeader = new byte[] { 137, 80, 78, 71 };
    private readonly byte[] _webpHeader = Encoding.UTF8.GetBytes("RIFF");

    private readonly string _icoMediaType = "image/x-icon";
    private readonly string _icoAltMediaType = "image/vnd.microsoft.icon";
    private readonly byte[] _icoHeader = new byte[] { 00, 00, 01, 00 };

    private readonly string _jpegMediaType = "image/jpeg";
    private readonly byte[] _jpegHeader = new byte[] { 255, 216, 255 };

    private readonly HashSet<string> _allowedMediaTypes;
    private readonly HttpClient _httpClient;
    private readonly ILogger<IIconFetchingService> _logger;

    public IconFetchingService(ILogger<IIconFetchingService> logger)
    {
        _logger = logger;
        _allowedMediaTypes = new HashSet<string>
        {
            _pngMediaType,
            _icoMediaType,
            _icoAltMediaType,
            _jpegMediaType
        };

        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        });
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _httpClient.MaxResponseContentBufferSize = 5000000; // 5 MB
    }

    public async Task<IconResult> GetIconAsync(string domain)
    {
        if (IPAddress.TryParse(domain, out _))
        {
            _logger.LogWarning("IP address: {0}.", domain);
            return null;
        }

        if (!Uri.TryCreate($"https://{domain}", UriKind.Absolute, out var parsedHttpsUri))
        {
            _logger.LogWarning("Bad domain: {0}.", domain);
            return null;
        }

        var uri = parsedHttpsUri;
        var response = await GetAndFollowAsync(uri, 2);
        if ((response == null || !response.IsSuccessStatusCode) &&
            Uri.TryCreate($"http://{parsedHttpsUri.Host}", UriKind.Absolute, out var parsedHttpUri))
        {
            Cleanup(response);
            uri = parsedHttpUri;
            response = await GetAndFollowAsync(uri, 2);

            if (response == null || !response.IsSuccessStatusCode)
            {
                var dotCount = domain.Count(c => c == '.');
                if (dotCount > 1 && DomainName.TryParseBaseDomain(domain, out var baseDomain) &&
                    Uri.TryCreate($"https://{baseDomain}", UriKind.Absolute, out var parsedBaseUri))
                {
                    Cleanup(response);
                    uri = parsedBaseUri;
                    response = await GetAndFollowAsync(uri, 2);
                }
                else if (dotCount < 2 &&
                    Uri.TryCreate($"https://www.{parsedHttpsUri.Host}", UriKind.Absolute, out var parsedWwwUri))
                {
                    Cleanup(response);
                    uri = parsedWwwUri;
                    response = await GetAndFollowAsync(uri, 2);
                }
            }
        }

        if (response?.Content == null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Couldn't load a website for {0}: {1}.", domain,
                response?.StatusCode.ToString() ?? "null");
            Cleanup(response);
            return null;
        }

        var parser = new HtmlParser();
        using (response)
        using (var htmlStream = await response.Content.ReadAsStreamAsync())
        using (var document = await parser.ParseDocumentAsync(htmlStream))
        {
            uri = response.RequestMessage.RequestUri;
            if (document.DocumentElement == null)
            {
                _logger.LogWarning("No DocumentElement for {0}.", domain);
                return null;
            }

            var baseUrl = "/";
            var baseUrlNode = document.QuerySelector("head base[href]");
            if (baseUrlNode != null)
            {
                var hrefAttr = baseUrlNode.Attributes["href"];
                if (!string.IsNullOrWhiteSpace(hrefAttr?.Value))
                {
                    baseUrl = hrefAttr.Value;
                }

                baseUrlNode = null;
                hrefAttr = null;
            }

            var icons = new List<IconResult>();
            var links = document.QuerySelectorAll("head link[href]");
            if (links != null)
            {
                foreach (var link in links.Take(200))
                {
                    var hrefAttr = link.Attributes["href"];
                    if (string.IsNullOrWhiteSpace(hrefAttr?.Value))
                    {
                        continue;
                    }

                    var relAttr = link.Attributes["rel"];
                    var sizesAttr = link.Attributes["sizes"];
                    if (relAttr != null && _iconRels.Contains(relAttr.Value.ToLower()))
                    {
                        icons.Add(new IconResult(hrefAttr.Value, sizesAttr?.Value));
                    }
                    else if (relAttr == null || !_blacklistedRels.Contains(relAttr.Value.ToLower()))
                    {
                        try
                        {
                            var extension = Path.GetExtension(hrefAttr.Value);
                            if (_iconExtensions.Contains(extension.ToLower()))
                            {
                                icons.Add(new IconResult(hrefAttr.Value, sizesAttr?.Value));
                            }
                        }
                        catch (ArgumentException) { }
                    }

                    sizesAttr = null;
                    relAttr = null;
                    hrefAttr = null;
                }

                links = null;
            }

            var iconResultTasks = new List<Task>();
            foreach (var icon in icons.OrderBy(i => i.Priority).Take(10))
            {
                Uri iconUri = null;
                if (icon.Path.StartsWith("//") && Uri.TryCreate($"{GetScheme(uri)}://{icon.Path.Substring(2)}",
                    UriKind.Absolute, out var slashUri))
                {
                    iconUri = slashUri;
                }
                else if (Uri.TryCreate(icon.Path, UriKind.Relative, out var relUri))
                {
                    iconUri = ResolveUri($"{GetScheme(uri)}://{uri.Host}", baseUrl, relUri.OriginalString);
                }
                else if (Uri.TryCreate(icon.Path, UriKind.Absolute, out var absUri))
                {
                    iconUri = absUri;
                }

                if (iconUri != null)
                {
                    var task = GetIconAsync(iconUri).ContinueWith(async (r) =>
                    {
                        var result = await r;
                        if (result != null)
                        {
                            icon.Path = iconUri.ToString();
                            icon.Icon = result.Icon;
                        }
                    });
                    iconResultTasks.Add(task);
                }
            }

            await Task.WhenAll(iconResultTasks);
            if (!icons.Any(i => i.Icon != null))
            {
                var faviconUri = ResolveUri($"{GetScheme(uri)}://{uri.Host}", "favicon.ico");
                var result = await GetIconAsync(faviconUri);
                if (result != null)
                {
                    icons.Add(result);
                }
                else
                {
                    _logger.LogWarning("No favicon.ico found for {0}.", uri.Host);
                    return null;
                }
            }

            return icons.Where(i => i.Icon != null).OrderBy(i => i.Priority).First();
        }
    }

    private async Task<IconResult> GetIconAsync(Uri uri)
    {
        using (var response = await GetAndFollowAsync(uri, 2))
        {
            if (response?.Content?.Headers == null || !response.IsSuccessStatusCode)
            {
                response?.Content?.Dispose();
                return null;
            }

            var format = response.Content.Headers?.ContentType?.MediaType;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            response.Content.Dispose();
            if (format == null || !_allowedMediaTypes.Contains(format))
            {
                if (HeaderMatch(bytes, _icoHeader))
                {
                    format = _icoMediaType;
                }
                else if (HeaderMatch(bytes, _pngHeader) || HeaderMatch(bytes, _webpHeader))
                {
                    format = _pngMediaType;
                }
                else if (HeaderMatch(bytes, _jpegHeader))
                {
                    format = _jpegMediaType;
                }
                else
                {
                    return null;
                }
            }

            return new IconResult(uri, bytes, format);
        }
    }

    private async Task<HttpResponseMessage> GetAndFollowAsync(Uri uri, int maxRedirectCount)
    {
        var response = await GetAsync(uri);
        if (response == null)
        {
            return null;
        }
        return await FollowRedirectsAsync(response, maxRedirectCount);
    }

    private async Task<HttpResponseMessage> GetAsync(Uri uri)
    {
        if (uri == null)
        {
            return null;
        }

        // Prevent non-http(s) and non-default ports
        if ((uri.Scheme != "http" && uri.Scheme != "https") || !uri.IsDefaultPort)
        {
            return null;
        }

        // Prevent local hosts (localhost, bobs-pc, etc) and IP addresses
        if (!uri.Host.Contains(".") || IPAddress.TryParse(uri.Host, out _))
        {
            return null;
        }

        // Resolve host to make sure it is not an internal/private IP address
        try
        {
            var hostEntry = Dns.GetHostEntry(uri.Host);
            if (hostEntry?.AddressList.Any(ip => IsInternal(ip)) ?? true)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        using (var message = new HttpRequestMessage())
        {
            message.RequestUri = uri;
            message.Method = HttpMethod.Get;

            // Let's add some headers to look like we're coming from a web browser request. Some websites
            // will block our request without these.
            message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36 Edge/16.16299");
            message.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            message.Headers.Add("Cache-Control", "no-cache");
            message.Headers.Add("Pragma", "no-cache");
            message.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;" +
                "q=0.9,image/webp,image/apng,*/*;q=0.8");

            try
            {
                return await _httpClient.SendAsync(message);
            }
            catch
            {
                return null;
            }
        }
    }

    private async Task<HttpResponseMessage> FollowRedirectsAsync(HttpResponseMessage response,
        int maxFollowCount, int followCount = 0)
    {
        if (response == null || response.IsSuccessStatusCode || followCount > maxFollowCount)
        {
            return response;
        }

        if (!(response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.MovedPermanently ||
            response.StatusCode == HttpStatusCode.RedirectKeepVerb ||
            response.StatusCode == HttpStatusCode.SeeOther) ||
            response.Headers.Location == null)
        {
            Cleanup(response);
            return null;
        }

        Uri location = null;
        if (response.Headers.Location.IsAbsoluteUri)
        {
            if (response.Headers.Location.Scheme != "http" && response.Headers.Location.Scheme != "https")
            {
                if (Uri.TryCreate($"https://{response.Headers.Location.OriginalString}",
                    UriKind.Absolute, out var newUri))
                {
                    location = newUri;
                }
            }
            else
            {
                location = response.Headers.Location;
            }
        }
        else
        {
            var requestUri = response.RequestMessage.RequestUri;
            location = ResolveUri($"{GetScheme(requestUri)}://{requestUri.Host}",
                response.Headers.Location.OriginalString);
        }

        Cleanup(response);
        var newResponse = await GetAsync(location);
        if (newResponse != null)
        {
            followCount++;
            var redirectedResponse = await FollowRedirectsAsync(newResponse, maxFollowCount, followCount);
            if (redirectedResponse != null)
            {
                if (redirectedResponse != newResponse)
                {
                    Cleanup(newResponse);
                }
                return redirectedResponse;
            }
        }

        return null;
    }

    private bool HeaderMatch(byte[] imageBytes, byte[] header)
    {
        return imageBytes.Length >= header.Length && header.SequenceEqual(imageBytes.Take(header.Length));
    }

    private Uri ResolveUri(string baseUrl, params string[] paths)
    {
        var url = baseUrl;
        foreach (var path in paths)
        {
            if (Uri.TryCreate(new Uri(url), path, out var r))
            {
                url = r.ToString();
            }
        }
        return new Uri(url);
    }

    private void Cleanup(IDisposable obj)
    {
        obj?.Dispose();
        obj = null;
    }

    private string GetScheme(Uri uri)
    {
        return uri != null && uri.Scheme == "http" ? "http" : "https";
    }

    public static bool IsInternal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var ipString = ip.ToString();
        if (ipString == "::1" || ipString == "::" || ipString.StartsWith("::ffff:"))
        {
            return true;
        }

        // IPv6
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ipString.StartsWith("fc") || ipString.StartsWith("fd") ||
                ipString.StartsWith("fe") || ipString.StartsWith("ff");
        }

        // IPv4
        var bytes = ip.GetAddressBytes();
        return (bytes[0]) switch
        {
            0 => true,
            10 => true,
            127 => true,
            169 => bytes[1] == 254, // Cloud environments, such as AWS
            172 => bytes[1] < 32 && bytes[1] >= 16,
            192 => bytes[1] == 168,
            _ => false,
        };
    }
}
