using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Response;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class ConfigControllerTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public ConfigControllerTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetConfigs()
    {
        var tokens = await _factory.LoginWithNewAccount();
        var client = _factory.CreateClient();

        using var message = new HttpRequestMessage(HttpMethod.Get, "/config");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ConfigResponseModel>();

        Assert.NotEmpty(content!.Version);
    }
}
