using System.Buffers.Binary;
using System.Net;
using System.Text;
using AngleSharp.Dom;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Models;

public class IconLinkTests
{
    private static readonly byte[] _pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

    private readonly IElement _element;
    private readonly Uri _uri = new("https://icon.test");
    private readonly ILogger<IIconFetchingService> _logger = Substitute.For<ILogger<IIconFetchingService>>();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUriService _uriService;
    private readonly string _baseUrlPath = "/";

    public IconLinkTests()
    {
        _element = Substitute.For<IElement>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _uriService = Substitute.For<IUriService>();
        _uriService.TryGetUri(Arg.Any<Uri>(), out Arg.Any<IconUri>()).Returns(x =>
        {
            x[1] = new IconUri(new Uri("https://icon.test"), IPAddress.Parse("192.0.2.1"));
            return true;
        });
    }

    [Fact]
    public void WithNoHref_IsNotUsable()
    {
        _element.GetAttribute("href").Returns(string.Empty);

        var result = new IconLink(_element, _uri, _baseUrlPath).IsUsable();

        Assert.False(result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("unusable", false)]
    [InlineData("ico", true)]
    public void WithNoRel_IsUsable(string? extension, bool expectedResult)
    {
        SetAttributeValue("href", $"/favicon.{extension}");

        var result = new IconLink(_element, _uri, _baseUrlPath).IsUsable();

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("icon", true)]
    [InlineData("stylesheet", false)]
    public void WithRel_IsUsable(string rel, bool expectedResult)
    {
        SetAttributeValue("href", "/favicon.ico");
        SetAttributeValue("rel", rel);

        var result = new IconLink(_element, _uri, _baseUrlPath).IsUsable();

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task FetchAsync_Unvalidated_ReturnsNull()
    {
        var result = await new IconLink(_element, _uri, _baseUrlPath).FetchAsync(_logger, _httpClientFactory, _uriService);

        Assert.Null(result);
    }

    [Fact]
    public void StripPngMetadata_RemovesDisallowedChunks_KeepsAllowedChunks()
    {
        var png = BuildPng(new[]
        {
            ("IHDR", new byte[13]),
            ("tEXt", Encoding.ASCII.GetBytes("Comment\0secret")),
            ("sRGB", new byte[] { 0 }),
            ("iTXt", Encoding.ASCII.GetBytes("evil")),
            ("IDAT", new byte[] { 1, 2, 3 }),
            ("zTXt", Encoding.ASCII.GetBytes("evil")),
            ("IEND", Array.Empty<byte>()),
        });

        var stripped = IconLink.StripPngMetadata(png);
        var chunkTypes = ReadChunkTypes(stripped);

        Assert.Equal(new[] { "IHDR", "sRGB", "IDAT", "IEND" }, chunkTypes);
        Assert.DoesNotContain("tEXt", chunkTypes);
        Assert.DoesNotContain("iTXt", chunkTypes);
        Assert.DoesNotContain("zTXt", chunkTypes);
    }

    [Fact]
    public void StripPngMetadata_StopsAtIend_DropsTrailingGarbage()
    {
        var png = BuildPng(new[]
        {
            ("IHDR", new byte[13]),
            ("IDAT", new byte[] { 9, 9 }),
            ("IEND", Array.Empty<byte>()),
        });
        var withTrailer = png.Concat(Encoding.ASCII.GetBytes("trailing-garbage")).ToArray();

        var stripped = IconLink.StripPngMetadata(withTrailer);

        Assert.Equal(png, stripped);
    }

    [Fact]
    public void StripPngMetadata_InvalidSignature_ReturnsInput()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var stripped = IconLink.StripPngMetadata(bytes);

        Assert.Same(bytes, stripped);
    }

    [Fact]
    public void StripPngMetadata_ChunkLengthOverflowsBuffer_ReturnsInput()
    {
        var prefix = _pngSignature.ToList();
        // Declare a chunk with absurdly large length but include the 12 bytes
        // required for the parser to enter the chunk-walk loop.
        prefix.AddRange(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF });
        prefix.AddRange(Encoding.ASCII.GetBytes("IHDR"));
        prefix.AddRange(new byte[] { 0, 0, 0, 0 });
        var bytes = prefix.ToArray();

        var stripped = IconLink.StripPngMetadata(bytes);

        Assert.Same(bytes, stripped);
    }

    [Fact]
    public void StripIcoEmbeddedPngMetadata_StripsPngEntries_RewritesOffsetsAndSizes()
    {
        var pngWithMetadata = BuildPng(new[]
        {
            ("IHDR", new byte[13]),
            ("tEXt", Encoding.ASCII.GetBytes("c\0attacker")),
            ("IDAT", new byte[] { 1, 2, 3 }),
            ("IEND", Array.Empty<byte>()),
        });
        var bmp = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };

        var ico = BuildIco(new[] { pngWithMetadata, bmp });

        var stripped = IconLink.StripIcoEmbeddedPngMetadata(ico);
        var entries = ReadIcoEntries(stripped);

        Assert.Equal(2, entries.Count);
        // First entry: PNG, metadata removed.
        var firstData = stripped.AsSpan(entries[0].Offset, entries[0].Size).ToArray();
        Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, ReadChunkTypes(firstData));
        Assert.True(firstData.Length < pngWithMetadata.Length);
        // Second entry: untouched BMP bytes.
        var secondData = stripped.AsSpan(entries[1].Offset, entries[1].Size).ToArray();
        Assert.Equal(bmp, secondData);
        // Offsets must point past the directory and pack contiguously.
        Assert.Equal(6 + 16 * 2, entries[0].Offset);
        Assert.Equal(entries[0].Offset + entries[0].Size, entries[1].Offset);
    }

    [Fact]
    public void StripIcoEmbeddedPngMetadata_NoPngEntries_PreservesData()
    {
        var bmpA = new byte[] { 1, 2, 3, 4 };
        var bmpB = new byte[] { 5, 6, 7, 8, 9 };

        var ico = BuildIco(new[] { bmpA, bmpB });
        var stripped = IconLink.StripIcoEmbeddedPngMetadata(ico);
        var entries = ReadIcoEntries(stripped);

        Assert.Equal(bmpA, stripped.AsSpan(entries[0].Offset, entries[0].Size).ToArray());
        Assert.Equal(bmpB, stripped.AsSpan(entries[1].Offset, entries[1].Size).ToArray());
    }

    [Fact]
    public void StripIcoEmbeddedPngMetadata_CorruptOffset_ReturnsInput()
    {
        var ico = BuildIco(new[] { new byte[] { 1, 2, 3, 4 } });
        // Point the entry data offset past end of buffer.
        BinaryPrimitives.WriteInt32LittleEndian(ico.AsSpan(6 + 12, 4), ico.Length + 1000);

        var stripped = IconLink.StripIcoEmbeddedPngMetadata(ico);

        Assert.Same(ico, stripped);
    }

    private static byte[] BuildPng(IEnumerable<(string Type, byte[] Data)> chunks)
    {
        using var stream = new MemoryStream();
        stream.Write(_pngSignature);
        foreach (var (type, data) in chunks)
        {
            var lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
            stream.Write(lengthBytes);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            // CRC placeholder — strip logic does not validate CRCs.
            stream.Write(new byte[] { 0, 0, 0, 0 });
        }
        return stream.ToArray();
    }

    private static List<string> ReadChunkTypes(byte[] png)
    {
        var types = new List<string>();
        var offset = _pngSignature.Length;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            types.Add(Encoding.ASCII.GetString(png, offset + 4, 4));
            offset += 12 + length;
        }
        return types;
    }

    private static byte[] BuildIco(IReadOnlyList<byte[]> images)
    {
        const int dirHeaderSize = 6;
        const int entrySize = 16;

        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0, 0, 1, 0 }); // reserved, type=icon
        var countBytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(countBytes, (ushort)images.Count);
        stream.Write(countBytes);

        var dataOffset = dirHeaderSize + entrySize * images.Count;
        foreach (var image in images)
        {
            var entry = new byte[entrySize];
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8, 4), image.Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(12, 4), dataOffset);
            stream.Write(entry);
            dataOffset += image.Length;
        }
        foreach (var image in images)
        {
            stream.Write(image);
        }
        return stream.ToArray();
    }

    private static List<(int Size, int Offset)> ReadIcoEntries(byte[] ico)
    {
        const int dirHeaderSize = 6;
        const int entrySize = 16;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4, 2));
        var entries = new List<(int Size, int Offset)>(count);
        for (var i = 0; i < count; i++)
        {
            var entryOffset = dirHeaderSize + i * entrySize;
            var size = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(entryOffset + 8, 4));
            var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(entryOffset + 12, 4));
            entries.Add((size, dataOffset));
        }
        return entries;
    }

    private void SetAttributeValue(string attribute, string value)
    {
        var attr = Substitute.For<IAttr>();
        attr.Value.Returns(value);

        _element.Attributes[attribute].Returns(attr);
    }
}
