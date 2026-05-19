#nullable enable

using System.Buffers.Binary;
using System.Text;
using AngleSharp.Dom;
using Bit.Icons.Extensions;
using Bit.Icons.Services;

namespace Bit.Icons.Models;

public class IconLink
{
    private static readonly HashSet<string> _iconRels = new(StringComparer.InvariantCultureIgnoreCase) { "icon", "apple-touch-icon", "shortcut icon" };
    private static readonly HashSet<string> _blocklistedRels = new(StringComparer.InvariantCultureIgnoreCase) { "preload", "image_src", "preconnect", "canonical", "alternate", "stylesheet" };
    private static readonly HashSet<string> _iconExtensions = new(StringComparer.InvariantCultureIgnoreCase) { ".ico", ".png", ".jpg", ".jpeg" };
    private const string _pngMediaType = "image/png";
    private static readonly byte[] _pngHeader = [137, 80, 78, 71];
    private static readonly byte[] _pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] _webpHeader = Encoding.UTF8.GetBytes("RIFF");

    private static readonly HashSet<string> _allowedPngChunkTypes = new(StringComparer.Ordinal)
    {
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "sRGB", "gAMA", "cHRM",
    };

    private const string _icoMediaType = "image/x-icon";
    private const string _icoAltMediaType = "image/vnd.microsoft.icon";
    private static readonly byte[] _icoHeader = new byte[] { 00, 00, 01, 00 };

    private const string _jpegMediaType = "image/jpeg";
    private static readonly byte[] _jpegHeader = new byte[] { 255, 216, 255 };

    private const string _svgXmlMediaType = "image/svg+xml";

    private static readonly HashSet<string> _allowedMediaTypes = new(StringComparer.InvariantCultureIgnoreCase)
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

        if (Rel != null && _iconRels.Contains(Rel.Value))
        {
            _validated = true;
        }
        if (Rel == null || !_blocklistedRels.Contains(Rel.Value))
        {
            try
            {
                var extension = Path.GetExtension(Href.Value);
                if (_iconExtensions.Contains(extension))
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
        response.Content.Dispose();
        if (format == null || !_allowedMediaTypes.Contains(format))
        {
            format = DetermineImageFormatFromFile(bytes);
        }

        if (format == null || !_allowedMediaTypes.Contains(format))
        {
            return null;
        }

        if (HeaderMatch(bytes, _pngHeader))
        {
            bytes = StripPngMetadata(bytes);
        }
        else if (HeaderMatch(bytes, _icoHeader))
        {
            bytes = StripIcoEmbeddedPngMetadata(bytes);
        }

        if (bytes == null)
        {
            return null;
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

    internal static byte[]? StripPngMetadata(byte[] bytes)
    {
        if (bytes.Length < _pngSignature.Length || !HeaderMatch(bytes, _pngSignature))
        {
            return null;
        }

        using var output = new MemoryStream(bytes.Length);
        output.Write(bytes, 0, _pngSignature.Length);

        var offset = _pngSignature.Length;
        var seenIend = false;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length < 0 || (long)offset + 12 + length > bytes.Length)
            {
                return null;
            }

            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var chunkSize = 12 + length;

            if (_allowedPngChunkTypes.Contains(type))
            {
                output.Write(bytes, offset, chunkSize);
            }

            offset += chunkSize;

            if (type == "IEND")
            {
                seenIend = true;
                break;
            }
        }

        if (!seenIend)
        {
            return null;
        }

        return output.ToArray();
    }

    internal static byte[]? StripIcoEmbeddedPngMetadata(byte[] bytes)
    {
        const int dirHeaderSize = 6;
        const int entrySize = 16;

        if (bytes.Length < dirHeaderSize || !HeaderMatch(bytes, _icoHeader))
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        var directorySize = dirHeaderSize + count * entrySize;
        if (count == 0 || bytes.Length < directorySize)
        {
            return null;
        }

        var entries = new byte[count][];
        var images = new byte[count][];

        for (var i = 0; i < count; i++)
        {
            var entryOffset = dirHeaderSize + i * entrySize;
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4));
            var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entryOffset + 12, 4));

            if (size < 0 || dataOffset < directorySize || (long)dataOffset + size > bytes.Length)
            {
                return null;
            }

            var image = new byte[size];
            Buffer.BlockCopy(bytes, dataOffset, image, 0, size);

            if (HeaderMatch(image, _pngHeader))
            {
                var stripped = StripPngMetadata(image);
                if (stripped == null)
                {
                    return null;
                }
                image = stripped;
            }

            var entry = new byte[entrySize];
            Buffer.BlockCopy(bytes, entryOffset, entry, 0, entrySize);

            entries[i] = entry;
            images[i] = image;
        }

        using var output = new MemoryStream(bytes.Length);
        output.Write(bytes, 0, dirHeaderSize);

        var imageOffset = directorySize;
        for (var i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(entries[i].AsSpan(8, 4), images[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(entries[i].AsSpan(12, 4), imageOffset);
            output.Write(entries[i], 0, entrySize);
            imageOffset += images[i].Length;
        }

        for (var i = 0; i < count; i++)
        {
            output.Write(images[i], 0, images[i].Length);
        }

        return output.ToArray();
    }
}
