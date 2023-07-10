using Bit.Icons.Services;
using Xunit;

namespace Bit.Icons.Test.Services;

public class IconFetchingServiceTests : ServiceTestBase<IconFetchingService>
{
    [Theory]
    [InlineData("www.twitter.com")] // https site
    [InlineData("www.google.com")] // https site
    [InlineData("neverssl.com")] // http site
    [InlineData("neopets.com")] // uses favicon.ico
    [InlineData("ameritrade.com")] // redirects to tdameritrace.com
    [InlineData("icloud.com")]
    [InlineData("bofa.com", Skip = "Broken in pipeline for .NET 6. Tracking link: https://bitwarden.atlassian.net/browse/PS-982")]
    public async Task GetIconAsync_Success(string domain)
    {
        var sut = BuildSut();
        var result = await sut.GetIconAsync(domain);

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("")]
    [InlineData("localhost")]
    public async Task GetIconAsync_ReturnsNull(string domain)
    {
        var sut = BuildSut();
        var result = await sut.GetIconAsync(domain);

        Assert.Null(result);
    }

    private IconFetchingService BuildSut() =>
        GetService<IconFetchingService>();
}
