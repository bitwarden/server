using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

/// <summary>
/// Covers service account access tokens issued while
/// <see cref="FeatureFlagKeys.Sm2093MachineAccountTokenPrefix"/> is enabled: the minted client
/// secret is "bw_"-prefixed, and it authenticates via the client_credentials grant end-to-end
/// through to an authenticated Secrets Manager API call.
/// </summary>
public class Sm2093MachineAccountTokenPrefixTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public Sm2093MachineAccountTokenPrefixTests(ApiApplicationFactory factory)
    {
        _factory = factory;

        // Force cloud mode; org sign-up requires a Free plan from PricingClient.
        _factory.UpdateConfiguration("globalSettings:selfHosted", "false");
        // The Identity host is a separate WebApplicationFactory instance with its own config
        // pipeline (it just happens to share the same SQLite database), so it needs the same
        // override for the client_credentials OAuth flow we drive against it below.
        _factory.Identity.UpdateConfiguration("globalSettings:selfHosted", "false");

        // The substitution must be registered before the host is built by CreateClient, so we
        // can force the flag on for every test in this class regardless of the LaunchDarkly
        // default.
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.Sm2093MachineAccountTokenPrefix)
                .Returns(true);
        });

        _client = _factory.CreateClient();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email);
        _organizationHelper = new SecretsManagerOrganizationHelper(_factory, _email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BwPrefixedAccessToken_AuthenticatesAndAuthorizes_EndToEnd()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var createTokenRequest = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = DateTime.UtcNow.AddDays(30),
        };

        var createTokenResponse = await _client.PostAsJsonAsync(
            $"/service-accounts/{serviceAccount.Id}/access-tokens", createTokenRequest);
        createTokenResponse.EnsureSuccessStatusCode();

        var accessToken = await createTokenResponse.Content.ReadFromJsonAsync<AccessTokenCreationResponseModel>();
        Assert.NotNull(accessToken);
        Assert.NotNull(accessToken.ClientSecret);
        Assert.StartsWith("bw_", accessToken.ClientSecret);

        var tokenContext = await _factory.Identity.ContextFromAccessTokenAsync(accessToken.Id, accessToken.ClientSecret!);

        Assert.Equal((int)HttpStatusCode.OK, tokenContext.Response.StatusCode);

        using var tokenBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(tokenContext);
        var tokenRoot = tokenBody.RootElement;
        Assert.True(tokenRoot.TryGetProperty("access_token", out var accessTokenJwt));
        var bearerToken = accessTokenJwt.GetString();
        Assert.False(string.IsNullOrWhiteSpace(bearerToken));
        Assert.True(tokenRoot.TryGetProperty("token_type", out var tokenType));

        using var authedRequest = new HttpRequestMessage(HttpMethod.Get, $"/organizations/{org.Id}/secrets/sync");
        authedRequest.Headers.Authorization = new AuthenticationHeaderValue(tokenType.GetString()!, bearerToken);

        var syncResponse = await _client.SendAsync(authedRequest);

        Assert.NotEqual(HttpStatusCode.Unauthorized, syncResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, syncResponse.StatusCode);
        syncResponse.EnsureSuccessStatusCode();

        var syncResult = await syncResponse.Content.ReadFromJsonAsync<SecretsSyncResponseModel>();
        Assert.NotNull(syncResult);
        Assert.True(syncResult.HasChanges);
        Assert.NotNull(syncResult.Secrets);
        Assert.Empty(syncResult.Secrets.Data);
    }

    [Fact]
    public async Task BwPrefixedAccessToken_ViaOrganizationHelperPath_AlsoAuthenticates()
    {
        // Authenticates via SecretsManagerOrganizationHelper + LoginHelper instead of a direct
        // POST to the access-tokens endpoint.
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();

        Assert.NotNull(apiKeyDetails.ClientSecret);
        Assert.StartsWith("bw_", apiKeyDetails.ClientSecret);

        await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets/sync");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretsSyncResponseModel>();
        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.NotNull(result.Secrets);
        Assert.Empty(result.Secrets.Data);
    }
}
