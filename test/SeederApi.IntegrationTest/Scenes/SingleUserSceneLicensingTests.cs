using System.Net;
using System.Text.Json;
using Bit.Core.Billing.Services;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Reproduces cloud QA/dev, where no licensing certificate is configured and constructing
/// <see cref="ILicensingService"/> throws.
/// </summary>
public class SingleUserSceneLicensingTests : IAsyncLifetime
{
    private const string Username = "username";
    private const string Password = "pass";

    private readonly SeederApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public SingleUserSceneLicensingTests()
    {
        _factory = new SeederApiApplicationFactory();
        _factory.ConfigureAuth(Username, Password);
        _factory.ConfigureServices(services =>
        {
            services.RemoveAll<ILicensingService>();
            services.AddSingleton<ILicensingService>(_ => throw new Exception("Invalid licensing certificate."));
        });
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeedEndpoint_CloudSingleUser_DoesNotConstructLicensingService(bool premium)
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleUserScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleUserScene.Request
            {
                Email = $"cloud-user-{Guid.NewGuid()}@bitwarden.com",
                Password = "asdfasdfasdf",
                Premium = premium
            })
        }, Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SeedEndpoint_CloudHost_SelfHostedPremium_ReturnsBadRequestWithoutCreatingUser()
    {
        var email = $"cloud-selfhost-{Guid.NewGuid()}@bitwarden.com";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(SingleUserScene),
            Arguments = JsonSerializer.SerializeToElement(new SingleUserScene.Request
            {
                Email = email,
                Password = "asdfasdfasdf",
                Premium = true,
                SelfHosted = true
            })
        }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cloud mode", await response.Content.ReadAsStringAsync());

        // The mangler prefixes the local part, so match on the suffix.
        var db = _factory.GetDatabaseContext();
        Assert.False(db.Users.Any(u => u.Email.EndsWith(email)));
    }
}
