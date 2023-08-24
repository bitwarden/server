using System.Net;
using AngleSharp.Dom;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Models;

public class IconLinkTests
{
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
    public void WithNoRel_IsUsable(string extension, bool expectedResult)
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
    public void FetchAsync_Unvalidated_ReturnsNull()
    {
        var result = new IconLink(_element, _uri, _baseUrlPath).FetchAsync(_logger, _httpClientFactory, _uriService);

        Assert.Null(result.Result);
    }

    private void SetAttributeValue(string attribute, string value)
    {
        var attr = Substitute.For<IAttr>();
        attr.Value.Returns(value);

        _element.Attributes[attribute].Returns(attr);
    }
}
