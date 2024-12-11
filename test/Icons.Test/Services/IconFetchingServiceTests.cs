using Bit.Icons.Services;
using Xunit;

namespace Bit.Icons.Test.Services;

public class IconFetchingServiceTests : ServiceTestBase<IconFetchingService>
{
    [Theory(Skip = "Run ad-hoc")]
    [InlineData("www.twitter.com")] // https site
    [InlineData("www.google.com")] // https site
    [InlineData("neverssl.com")] // http site
    [InlineData("neopets.com")] // uses favicon.ico
    [InlineData("hopin.com")] // uses svg+xml format
    [InlineData("tdameritrade.com")]
    [InlineData("icloud.com")]
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

    private IconFetchingService BuildSut() => GetService<IconFetchingService>();
}
