using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Core;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

/// <summary>
/// Covers <see cref="FeatureFlagKeys.MachineAccountTokenPrefix"/>, which prepends "bw_" to newly
/// minted machine account access tokens. The flag is only read when a token is created; the
/// validation path compares hashes and never sees the secret's shape. This exercises both sides of
/// that boundary through the real client_credentials flow against the Identity test host, which
/// shares a database with the Api host.
/// </summary>
public class AccessTokenPrefixTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IApiKeyRepository _apiKeyRepository;

    // Captured from the SubstituteService callback so the test body can flip the flag mid-test on
    // the same NSubstitute instance the running host resolves.
    private IFeatureService _featureService = null!;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public AccessTokenPrefixTests(ApiApplicationFactory factory)
    {
        _factory = factory;

        // Force cloud mode; org sign-up requires a Free plan from PricingClient. The Identity host
        // is a separate WebApplicationFactory with its own configuration, so it needs this too.
        _factory.UpdateConfiguration("globalSettings:selfHosted", "false");
        _factory.Identity.UpdateConfiguration("globalSettings:selfHosted", "false");

        // Start with the flag off so the first token minted below is indistinguishable from one
        // issued before this feature shipped. Registered before CreateClient builds the host.
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.MachineAccountTokenPrefix).Returns(false);
            _featureService = featureService;
        });

        _client = _factory.CreateClient();
        _apiKeyRepository = _factory.GetService<IApiKeyRepository>();
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
    public async Task EnablingPrefix_PrefixesNewTokens_AndLeavesExistingTokensUsable()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);

        var legacyToken = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        Assert.DoesNotContain("bw_", legacyToken.ClientSecret, StringComparison.Ordinal);

        _featureService.IsEnabled(FeatureFlagKeys.MachineAccountTokenPrefix).Returns(true);

        var prefixedToken = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        Assert.StartsWith("bw_", prefixedToken.ClientSecret, StringComparison.Ordinal);

        // The prefixed secret must survive the client_credentials round trip. This is the part no
        // unit test can cover, since validation lives outside the creation command.
        await AssertAuthenticatesAsync(prefixedToken, org.Id);

        // Tokens issued before the flag was enabled keep working.
        await AssertAuthenticatesAsync(legacyToken, org.Id);

        await _loginHelper.LoginAsync(_email);
        var revokeResponse = await _client.PostAsJsonAsync(
            $"/service-accounts/{legacyToken.ApiKey.ServiceAccountId!.Value}/access-tokens/revoke",
            new RevokeAccessTokensRequest { Ids = new[] { legacyToken.ApiKey.Id } });
        revokeResponse.EnsureSuccessStatusCode();

        Assert.Null(await _apiKeyRepository.GetDetailsByIdAsync(legacyToken.ApiKey.Id));
    }

    private async Task AssertAuthenticatesAsync(ApiKeyClientSecretDetails token, Guid organizationId)
    {
        var tokenContext =
            await _factory.Identity.ContextFromAccessTokenAsync(token.ApiKey.Id, token.ClientSecret);
        Assert.Equal((int)HttpStatusCode.OK, tokenContext.Response.StatusCode);

        using var tokenBody = await JsonSerializer.DeserializeAsync<JsonDocument>(tokenContext.Response.Body);
        Assert.NotNull(tokenBody);
        Assert.True(tokenBody.RootElement.TryGetProperty("access_token", out var accessTokenElement));
        var bearerToken = accessTokenElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(bearerToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/organizations/{organizationId}/secrets/sync");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var syncResponse = await _client.SendAsync(request);
        syncResponse.EnsureSuccessStatusCode();
    }
}
