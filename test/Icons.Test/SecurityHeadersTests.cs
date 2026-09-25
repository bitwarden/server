using Xunit;

namespace Bit.Icons.Test;

public sealed class SecurityHeadersTests : IAsyncDisposable
{
    private readonly IconsApplicationFactory _factory = new();

    [Fact]
    public async Task GetAlive_ReturnsXFrameOptions()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/alive");
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("x-frame-options").Single());
    }

    [Fact]
    public async Task GetAlive_ReturnsXXssProtection()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/alive");
        Assert.Equal("1; mode=block", response.Headers.GetValues("x-xss-protection").Single());
    }

    [Fact]
    public async Task GetAlive_ReturnsXContentTypeOptions()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/alive");
        Assert.Equal("nosniff", response.Headers.GetValues("x-content-type-options").Single());
    }

    [Fact]
    public async Task GetAlive_ReturnsContentSecurityPolicy()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/alive");
        Assert.Equal(
            "default-src 'self'; script-src 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task GetAlive_ReturnsCacheControl()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/alive");
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Public);
        Assert.Equal(TimeSpan.FromDays(7), cacheControl.MaxAge);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
