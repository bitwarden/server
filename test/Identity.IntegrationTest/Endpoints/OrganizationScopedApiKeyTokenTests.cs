using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

public class OrganizationScopedApiKeyTokenTests
    : IClassFixture<OrganizationScopedApiKeyTokenTests.ScopedApiKeysEnabledIdentityApplicationFactory>
{
    private const string FlagSettingKey =
        $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.ScopedOrganizationApiKeys}";

    private readonly ScopedApiKeysEnabledIdentityApplicationFactory _factory;

    public OrganizationScopedApiKeyTokenTests(ScopedApiKeysEnabledIdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_ReceivesOnlyItsScopes(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var scope = AssertHelper.AssertJsonProperty(body.RootElement, "scope", JsonValueKind.String).GetString();
        Assert.Equal(ApiScopes.ApiOrganizationEventsRead, scope);
        var expiresIn = AssertHelper.AssertJsonProperty(body.RootElement, "expires_in", JsonValueKind.Number).GetInt32();
        Assert.Equal(3600, expiresIn);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_WithoutRequestedScope_ReceivesAllOfItsScopesAndNotApiOrganization(
        Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret, scope: null);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var token = await ReadAccessTokenAsync(context);
        var scopes = token.Claims.Where(c => c.Type == "scope").Select(c => c.Value).Order().ToArray();
        Assert.Equal<string>([ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead], scopes);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_RequestingEveryCatalogScopeByName_ReceivesThem(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [.. ApiScopes.OrganizationApiKeyScopes]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            string.Join(' ', ApiScopes.OrganizationApiKeyScopes));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var token = await ReadAccessTokenAsync(context);
        var scopes = token.Claims.Where(c => c.Type == "scope").Select(c => c.Value).Order().ToArray();
        Assert.Equal<string>(ApiScopes.OrganizationApiKeyScopes.Order(), scopes);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_RequestingApiOrganization_Fails(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganization);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(body.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_scope", error);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_StoredApiOrganizationScope_IsNotGranted(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganization]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret, scope: null);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var token = await ReadAccessTokenAsync(context);
        var scope = Assert.Single(token.Claims, c => c.Type == "scope");
        Assert.Equal(ApiScopes.ApiOrganizationEventsRead, scope.Value);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_TokenIdentifiesOrganizationAndKey(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory,
            $"organization.{organization.Id.ToString().ToUpperInvariant()}.{key.Id}", secret,
            ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var token = await ReadAccessTokenAsync(context);
        Assert.Equal(ClientId(organization.Id, key.Id), token.GetClaim("client_id").Value);
        Assert.Equal(organization.Id.ToString(), token.GetClaim("client_sub").Value);
        Assert.Equal(nameof(IdentityClientType.Organization), token.GetClaim("client_type").Value);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_OtherOrganizationIdInClientId_ReturnsInvalidClient(
        Organization organization, Organization otherOrganization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        otherOrganization = await CreateOrganizationAsync(_factory, otherOrganization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(otherOrganization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_Expired_ReturnsInvalidClient(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead], expireAt: DateTime.UtcNow.AddMinutes(-1));

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_Deleted_ReturnsInvalidClient(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);
        await _factory.Services.GetRequiredService<IOrganizationScopedApiKeyRepository>().DeleteAsync(key);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_WrongSecret_ReturnsInvalidClient(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        var (key, _) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), "wrong-secret",
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Fact]
    public async Task ScopedKey_UnknownOrganizationAndKey_ReturnsInvalidClient()
    {
        var context = await PostTokenAsync(_factory, ClientId(Guid.NewGuid(), Guid.NewGuid()), "secret",
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_OrganizationWithoutUseApi_ReturnsInvalidClient(Organization organization)
    {
        organization = await CreateOrganizationAsync(_factory, organization, useApi: false);
        var (key, secret) = await CreateScopedKeyAsync(_factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task ScopedKey_FlagOff_ReturnsInvalidClient(Organization organization)
    {
        var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration(FlagSettingKey, "false");
        organization = await CreateOrganizationAsync(factory, organization);
        var (key, secret) = await CreateScopedKeyAsync(factory, organization.Id,
            [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(factory, ClientId(organization.Id, key.Id), secret,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    [Theory, BitAutoData]
    public async Task LegacyKey_FlagOn_ReceivesApiOrganization(
        Organization organization, Bit.Core.Entities.OrganizationApiKey organizationApiKey)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;
        await _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>().CreateAsync(organizationApiKey);
        await CreateScopedKeyAsync(_factory, organization.Id, [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, $"organization.{organization.Id}", organizationApiKey.ApiKey,
            ApiScopes.ApiOrganization);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var token = await ReadAccessTokenAsync(context);
        Assert.Equal($"organization.{organization.Id}", token.GetClaim("client_id").Value);
        var scope = Assert.Single(token.Claims, c => c.Type == "scope");
        Assert.Equal(ApiScopes.ApiOrganization, scope.Value);
    }

    [Theory, BitAutoData]
    public async Task LegacyKeySecret_WithScopedClientId_ReturnsInvalidClient(
        Organization organization, Bit.Core.Entities.OrganizationApiKey organizationApiKey)
    {
        organization = await CreateOrganizationAsync(_factory, organization);
        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;
        await _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>().CreateAsync(organizationApiKey);
        var (key, _) = await CreateScopedKeyAsync(_factory, organization.Id, [ApiScopes.ApiOrganizationEventsRead]);

        var context = await PostTokenAsync(_factory, ClientId(organization.Id, key.Id), organizationApiKey.ApiKey,
            ApiScopes.ApiOrganizationEventsRead);

        await AssertInvalidClientAsync(context);
    }

    private static string ClientId(Guid organizationId, Guid keyId) => $"organization.{organizationId}.{keyId}";

    private static async Task<Organization> CreateOrganizationAsync(
        IdentityApplicationFactory factory, Organization organization, bool useApi = true)
    {
        organization.Enabled = true;
        organization.UseApi = useApi;
        return await factory.Services.GetRequiredService<IOrganizationRepository>().CreateAsync(organization);
    }

    private static async Task<(OrganizationScopedApiKey Key, string Secret)> CreateScopedKeyAsync(
        IdentityApplicationFactory factory, Guid organizationId, string[] scopes, DateTime? expireAt = null)
    {
        var secret = Guid.NewGuid().ToString("N");
        var key = await factory.Services.GetRequiredService<IOrganizationScopedApiKeyRepository>().CreateAsync(
            new OrganizationScopedApiKey
            {
                OrganizationId = organizationId,
                Name = "Test key",
                ClientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret))),
                Scopes = JsonSerializer.Serialize(scopes),
                ExpireAt = expireAt,
            });
        return (key, secret);
    }

    private static Task<HttpContext> PostTokenAsync(
        IdentityApplicationFactory factory, string clientId, string clientSecret, string? scope)
    {
        var form = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret },
        };
        if (scope != null)
        {
            form["scope"] = scope;
        }

        return factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    private static async Task<JsonWebToken> ReadAccessTokenAsync(HttpContext context)
    {
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var accessToken = AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String)
            .GetString();
        return new JsonWebToken(accessToken);
    }

    private static async Task AssertInvalidClientAsync(HttpContext context)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("error", property.Name);
        Assert.Equal("invalid_client", property.Value.GetString());
    }

    public sealed class ScopedApiKeysEnabledIdentityApplicationFactory : IdentityApplicationFactory
    {
        public ScopedApiKeysEnabledIdentityApplicationFactory() => UpdateConfiguration(FlagSettingKey, "true");
    }
}
