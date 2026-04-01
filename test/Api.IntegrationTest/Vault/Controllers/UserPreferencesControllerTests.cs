using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Vault.Controllers;

public class UserPreferencesControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _encryptedData =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _updatedEncryptedData =
        "2.06CDSJjTZaigYHUuswIq5A==|trxgZl2RCkYrrmCvGE9WNA==|w5p05eI5wsaYeSyWtsAPvBX63vj798kIMxBTfSB0BQg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private string _ownerEmail = null!;

    public UserPreferencesControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.SyncUserPreferences).Returns(true);
        });
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
        var model = new UpdateUserPreferencesRequestModel { Data = _encryptedData };

        var response = await _client.PostAsJsonAsync("/user-preferences", model);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(_encryptedData, result.Data);
    }

    [Fact]
    public async Task Create_ThenGet_ReturnsPreferences()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = _encryptedData };

        var createResponse = await _client.PostAsJsonAsync("/user-preferences", model);
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/user-preferences");
        getResponse.EnsureSuccessStatusCode();

        var result = await getResponse.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(_encryptedData, result.Data);
    }

    [Fact]
    public async Task Update_NoExistingPreferences_ReturnsNotFound()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = _updatedEncryptedData };

        var response = await _client.PutAsJsonAsync("/user-preferences", model);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingPreferences_ReturnsUpdatedPreferences()
    {
        var createModel = new UpdateUserPreferencesRequestModel { Data = _encryptedData };
        var createResponse = await _client.PostAsJsonAsync("/user-preferences", createModel);
        createResponse.EnsureSuccessStatusCode();

        var updateModel = new UpdateUserPreferencesRequestModel { Data = _updatedEncryptedData };
        var updateResponse = await _client.PutAsJsonAsync("/user-preferences", updateModel);
        updateResponse.EnsureSuccessStatusCode();

        var result = await updateResponse.Content.ReadFromJsonAsync<UserPreferencesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(_updatedEncryptedData, result.Data);
    }

    [Fact]
    public async Task Delete_Success_PreferencesRemoved()
    {
        var model = new UpdateUserPreferencesRequestModel { Data = _encryptedData };
        var createResponse = await _client.PostAsJsonAsync("/user-preferences", model);
        createResponse.EnsureSuccessStatusCode();

        var deleteResponse = await _client.DeleteAsync("/user-preferences");
        deleteResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync("/user-preferences");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
