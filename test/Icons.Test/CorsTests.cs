using Xunit;

namespace Bit.Icons.Test;

public sealed class CloudCorsTests : IAsyncDisposable
{
    private const string VaultOrigin = "https://vault.example.com";

    private readonly IconsApplicationFactory _factory = new(selfHosted: false, vaultOrigin: VaultOrigin);

    [Theory]
    [InlineData(VaultOrigin)]
    [InlineData("file://")]
    [InlineData("bw-desktop-file://bundle")]
    [InlineData("https://bitwarden.com")]
    public async Task AllowedOrigin_ReturnsAccessControlAllowOrigin(string origin)
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/alive");
        request.Headers.TryAddWithoutValidation("Origin", origin);

        var response = await client.SendAsync(request);

        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UnknownOrigin_DoesNotReturnAccessControlAllowOrigin()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/alive");
        request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}

public sealed class SelfHostedCorsTests : IAsyncDisposable
{
    private const string VaultOrigin = "https://vault.selfhosted.example.com";

    private readonly IconsApplicationFactory _factory = new(selfHosted: true, vaultOrigin: VaultOrigin);

    [Theory]
    [InlineData(VaultOrigin)]
    [InlineData("file://")]
    [InlineData("bw-desktop-file://bundle")]
    public async Task AllowedOrigin_ReturnsAccessControlAllowOrigin(string origin)
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/alive");
        request.Headers.TryAddWithoutValidation("Origin", origin);

        var response = await client.SendAsync(request);

        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task BitwardenComOrigin_DoesNotReturnAccessControlAllowOrigin()
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/alive");
        request.Headers.TryAddWithoutValidation("Origin", "https://bitwarden.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
