using System.Text.Json;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SelfHostedSingleUserSceneLicensingTests : IAsyncLifetime
{
    private const string Username = "username";
    private const string Password = "pass";

    private readonly SeederApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public SelfHostedSingleUserSceneLicensingTests()
    {
        _factory = new SeederApiApplicationFactory();
        _factory.ConfigureAuth(Username, Password);
        _factory.UpdateConfiguration("globalSettings:selfHosted", "true");
        // AddPush rejects self-hosted startup without an installation id.
        _factory.UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(Username, Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SeedEndpoint_SelfHostedPremiumUser_WithoutSigningCert_SkipsLicenseWithWarning()
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleUserScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleUserScene.Request
            {
                Email = $"premium-selfhost-{Guid.NewGuid()}@bitwarden.com",
                Password = "asdfasdfasdf",
                Premium = true,
                SelfHosted = true
            })
        }, Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SceneResponseModel>();

        Assert.NotNull(result);
        var sceneResult = Assert.IsType<JsonElement>(result.Result);
        Assert.False(sceneResult.GetProperty("premiumLicenseWritten").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(sceneResult.GetProperty("premiumLicenseWarning").GetString()));
    }
}
