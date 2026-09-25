using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Bit.Icons.Test;

public sealed class InfoEndpointTests : IAsyncDisposable
{
    private readonly IconsApplicationFactory _factory = new();

    [Theory]
    [InlineData("/alive")]
    [InlineData("/now")]
    public async Task GetAliveOrNow_Returns200WithDateTime(string path)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(DateTime.TryParse(body.Trim('"'), out _));
    }

    [Fact]
    public async Task GetVersion_Returns200WithParsableVersion()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<string>();
        Assert.True(Version.TryParse(version, out _));
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
