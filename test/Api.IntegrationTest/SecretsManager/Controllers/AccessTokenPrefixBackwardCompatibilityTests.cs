using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Core;
using Bit.Core.SecretsManager.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

/// <summary>
/// SM-2093 QA: proves, end-to-end, that a machine account (service account) access token issued
/// BEFORE the <see cref="FeatureFlagKeys.Sm2093MachineAccountTokenPrefix"/> flag is enabled keeps
/// authenticating exactly as before once the flag goes live for newly-created tokens. This is the
/// single most important non-breaking-change requirement for the whole epic: customers have
/// millions of these tokens already baked into CI/CD pipelines and must never need to rotate them.
///
/// Unlike the other ServiceAccountsControllerTests in this folder, this test drives a real OAuth
/// client_credentials request against the Identity test host (via <see cref="ApiApplicationFactory.Identity"/>,
/// which shares the same SQLite database as the Api host) rather than only exercising the SM Api
/// surface, so it empirically proves hash-based secret validation is content-agnostic rather than
/// just trusting that claim from static analysis.
/// </summary>
public class AccessTokenPrefixBackwardCompatibilityTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IApiKeyRepository _apiKeyRepository;

    // Captured from the SubstituteService callback below so the test body can flip the flag's
    // return value mid-test, on the exact same NSubstitute instance the running host resolves.
    private IFeatureService _featureServiceMock = null!;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public AccessTokenPrefixBackwardCompatibilityTests(ApiApplicationFactory factory)
    {
        _factory = factory;

        // QA-environment workaround: this machine's real local dev `dotnet user-secrets` for the
        // Api/Identity projects (used for its own SQL Server dev instance) sets
        // globalSettings:selfHosted=true. WebApplicationFactoryBase unconditionally loads those
        // same user secrets into every SQLite-backed test host, which otherwise makes
        // organization sign-up fail with "Could not find plan for type Free" (PricingClient
        // short-circuits to null when SelfHosted is true) -- unrelated to SM-2093. Force it back
        // to cloud mode for this test host only.
        _factory.UpdateConfiguration("globalSettings:selfHosted", "false");
        // The Identity host is a separate WebApplicationFactory instance with its own config
        // pipeline (it just happens to share the same SQLite database), so it needs the same
        // override for the client_credentials OAuth flow we drive against it below.
        _factory.Identity.UpdateConfiguration("globalSettings:selfHosted", "false");

        // Start with the flag OFF. Any access token minted before we flip it below is, by
        // construction, indistinguishable from a real token issued before SM-2093 shipped.
        // The substitution must be registered before CreateClient() builds the host.
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.Sm2093MachineAccountTokenPrefix).Returns(false);
            _featureServiceMock = featureService;
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
    public async Task OldUnprefixedAccessToken_StillAuthenticatesAndRevokes_AfterFlagEnabledForNewTokens()
    {
        // --- Arrange: mint a token the OLD way (flag disabled == simulates a pre-SM-2093 token) ---
        var (org, _) = await _organizationHelper.Initialize(true, true, true);

        var oldToken = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();

        Assert.NotNull(oldToken.ClientSecret);
        Assert.DoesNotContain("bw_", oldToken.ClientSecret, StringComparison.Ordinal);

        // --- Act: flip the flag ON for the rest of the test. This simulates the flag being ---
        // --- turned on in production sometime after the old token above was already issued.  ---
        _featureServiceMock.IsEnabled(FeatureFlagKeys.Sm2093MachineAccountTokenPrefix).Returns(true);

        // Sanity check the flip actually took effect for *new* tokens, without touching the old one.
        var newToken = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        Assert.StartsWith("bw_", newToken.ClientSecret, StringComparison.Ordinal);

        // --- Act: authenticate with the OLD, unprefixed client secret via a real OAuth ---
        // --- client_credentials request against the Identity test host.                 ---
        var tokenContext = await _factory.Identity.ContextFromAccessTokenAsync(oldToken.ApiKey.Id, oldToken.ClientSecret);

        // --- Assert: the old token still authenticates successfully (200 + a real access token). ---
        Assert.Equal((int)HttpStatusCode.OK, tokenContext.Response.StatusCode);

        using var tokenBody = await JsonSerializer.DeserializeAsync<JsonDocument>(tokenContext.Response.Body);
        Assert.True(tokenBody!.RootElement.TryGetProperty("access_token", out var accessTokenElement));
        var bearerToken = accessTokenElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(bearerToken));

        // --- Assert: the full round trip works -- use the resulting bearer token against a real, ---
        // --- authenticated Secrets Manager endpoint gated to service accounts.                    ---
        await _loginHelper.LoginWithApiKeyAsync(oldToken);
        var syncResponse = await _client.GetAsync($"/organizations/{org.Id}/secrets/sync");
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        // --- Assert: revoking this old-format token still works normally -- prefixing does not ---
        // --- change revocation behavior.                                                        ---
        await _loginHelper.LoginAsync(_email);
        var revokeResponse = await _client.PostAsJsonAsync(
            $"/service-accounts/{oldToken.ApiKey.ServiceAccountId!.Value}/access-tokens/revoke",
            new RevokeAccessTokensRequest { Ids = new[] { oldToken.ApiKey.Id } });
        revokeResponse.EnsureSuccessStatusCode();

        var revokedDetails = await _apiKeyRepository.GetDetailsByIdAsync(oldToken.ApiKey.Id);
        Assert.Null(revokedDetails);
    }
}
