using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class SelfHostedSingleUserSceneLicensingTests : IClassFixture<SelfHostedSeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public SelfHostedSingleUserSceneLicensingTests(SelfHostedSeederApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.SetBasicAuthentication(factory.Username, factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
    }

    [Fact]
    public async Task SeedEndpoint_SelfHostedPremiumUser_WithoutSigningCert_SkipsLicenseWithWarning()
    {
        var sceneResult = await _client.PostSceneAsync(Guid.NewGuid().ToString(), nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"premium-selfhost-{Guid.NewGuid()}@bitwarden.com",
            Password = "asdfasdfasdf",
            Premium = true,
            SelfHosted = true
        });

        Assert.False(sceneResult.GetProperty("premiumLicenseWritten").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(sceneResult.GetProperty("premiumLicenseWarning").GetString()));
    }
}
