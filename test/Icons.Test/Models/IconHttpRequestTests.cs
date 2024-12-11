using System.Net;
using Bit.Icons.Models;
using Bit.Icons.Services;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Xunit;

namespace Bit.Icons.Test.Models;

public class IconHttpRequestTests
{
    [Fact]
    public async Task FetchAsync_FollowsTwoRedirectsAsync()
    {
        var handler = new MockedHttpMessageHandler();

        var request = handler
            .Fallback.WithStatusCode(HttpStatusCode.Redirect)
            .WithContent(
                "text/html",
                "<html><head><title>Redirect 2</title></head><body><a href=\"https://icon.test\">Redirect 3</a></body></html>"
            )
            .WithHeader(HeaderNames.Location, "https://icon.test");

        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient("Icons").Returns(handler.ToHttpClient());

        var uriService = Substitute.For<IUriService>();
        uriService
            .TryGetUri(Arg.Any<Uri>(), out Arg.Any<IconUri>())
            .Returns(x =>
            {
                x[1] = new IconUri(new Uri("https://icon.test"), IPAddress.Parse("192.0.2.1"));
                return true;
            });
        var result = await IconHttpRequest.FetchAsync(
            new Uri("https://icon.test"),
            NullLogger<IIconFetchingService>.Instance,
            clientFactory,
            uriService
        );

        Assert.Equal(3, request.NumberOfResponses); // Initial + 2 redirects
    }
}
