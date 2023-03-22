using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core.Entities;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class ConfigControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    private string _email = null!;

    public ConfigControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";

        var tokens = await _factory.LoginWithNewAccount(_email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task LoginAsync()
    {
        var tokens = await _factory.LoginAsync(_email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    [Fact]
    public async Task GetConfigs()
    {
        var response = await _client.GetAsync("/config");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfigResponseModel>();

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Version);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task GetConfigs_WithOrganizations(int orgCount)
    {
        for (var i = 0; i < orgCount; i++)
        {
            var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
            await _factory.LoginWithNewAccount(ownerEmail);

            Organization org;
            (org, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: Core.Enums.PlanType.Free, ownerEmail: ownerEmail,
                name: i.ToString(), billingEmail: ownerEmail, ownerKey: i.ToString());
            await OrganizationTestHelpers.CreateUserAsync(_factory, org.Id, _email, Core.Enums.OrganizationUserType.User);
        }

        await LoginAsync();

        var response = await _client.GetAsync("/config");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfigResponseModel>();

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Version);
    }
}
