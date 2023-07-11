using System.Net;
using AngleSharp.Html.Parser;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Bit.Test.Common.Helpers;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Models;

public class IconHttpResponseTests
{
    private readonly IUriService _mockedUriService;
    private static readonly IHtmlParser _parser = new HtmlParser();

    public IconHttpResponseTests()
    {
        _mockedUriService = Substitute.For<IUriService>();
        _mockedUriService.TryGetUri(Arg.Any<Uri>(), out Arg.Any<IconUri>()).Returns(x =>
        {
            x[1] = new IconUri(new Uri("https://test.local"), IPAddress.Parse("3.0.0.0"));
            return true;
        });
    }

    [Fact]
    public async Task RetrieveIconsAsync_Processes200LinksAsync()
    {
        var htmlBuilder = new HtmlBuilder();
        var headBuilder = new HtmlBuilder("head");
        for (var i = 0; i < 200; i++)
        {
            headBuilder.Append(UnusableLinkNode());
        }
        headBuilder.Append(UsableLinkNode());
        htmlBuilder.Append(headBuilder);
        var response = GetHttpResponseMessage(htmlBuilder.ToString());
        var sut = CurriedIconHttpResponse()(response);

        var result = await sut.RetrieveIconsAsync(new Uri("https://test.local"), "test.local", _parser);

        Assert.Empty(result);
    }

    [Fact]
    public async Task RetrieveIconsAsync_Processes10IconsAsync()
    {
        var htmlBuilder = new HtmlBuilder();
        var headBuilder = new HtmlBuilder("head");
        for (var i = 0; i < 11; i++)
        {
            headBuilder.Append(UsableLinkNode());
        }
        htmlBuilder.Append(headBuilder);
        var response = GetHttpResponseMessage(htmlBuilder.ToString());
        var sut = CurriedIconHttpResponse()(response);

        var result = await sut.RetrieveIconsAsync(new Uri("https://test.local"), "test.local", _parser);

        Assert.Equal(10, result.Count());
    }

    private static string UsableLinkNode()
    {
        return "<link rel=\"icon\" href=\"https://test.local/favicon.ico\" />";
    }

    private static string UnusableLinkNode()
    {
        // Empty href links are not usable
        return "<link rel=\"icon\" href=\"\" />";
    }

    private static HttpResponseMessage GetHttpResponseMessage(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://test.local"),
            Content = new StringContent(content)
        };
    }

    private Func<HttpResponseMessage, IconHttpResponse> CurriedIconHttpResponse()
    {
        return (HttpResponseMessage response) => new IconHttpResponse(response, NullLogger<IIconFetchingService>.Instance, UsableIconHttpClientFactory(), _mockedUriService);
    }

    private static IHttpClientFactory UsableIconHttpClientFactory()
    {
        var substitute = Substitute.For<IHttpClientFactory>();
        var handler = new MockedHttpMessageHandler();
        handler.Fallback
            .WithStatusCode(HttpStatusCode.OK)
            .WithContent("image/png", new byte[] { 137, 80, 78, 71 });

        substitute.CreateClient("Icons").Returns(handler.ToHttpClient());
        return substitute;
    }
}
