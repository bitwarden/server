using System.Net;
using System.Net.Http.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class UserPreferencesControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private string _ownerEmail = null!;

    public UserPreferencesControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_NoPreferences_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/user-preferences");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Success_ReturnsCreatedPreferences()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = "encrypted-preferences-data" };

        var response = await _client.PostAsJsonAsync("/user-preferences", model);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal("encrypted-preferences-data", result.Data);
    }

    [Fact]
    public async Task Create_ThenGet_ReturnsPreferences()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = "encrypted-preferences-data" };

        var createResponse = await _client.PostAsJsonAsync("/user-preferences", model);
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/user-preferences");
        getResponse.EnsureSuccessStatusCode();

        var result = await getResponse.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal("encrypted-preferences-data", result.Data);
    }

    [Fact]
    public async Task Update_NoExistingPreferences_ReturnsNotFound()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = "updated-data" };

        var response = await _client.PutAsJsonAsync("/user-preferences", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingPreferences_ReturnsUpdatedPreferences()
    {
        var createModel = new UpdateUserPreferencesRequestModel { Data = "original-data" };
        var createResponse = await _client.PostAsJsonAsync("/user-preferences", createModel);
        createResponse.EnsureSuccessStatusCode();

        var updateModel = new UpdateUserPreferencesRequestModel { Data = "updated-data" };
        var updateResponse = await _client.PutAsJsonAsync("/user-preferences", updateModel);
        updateResponse.EnsureSuccessStatusCode();

        var result = await updateResponse.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal("updated-data", result.Data);
    }

    [Fact]
    public async Task Delete_Success_PreferencesRemoved()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = "encrypted-data" };
        var createResponse = await _client.PostAsJsonAsync("/user-preferences", model);
        createResponse.EnsureSuccessStatusCode();

        var deleteResponse = await _client.DeleteAsync("/user-preferences");
        deleteResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/user-preferences");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
