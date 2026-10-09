using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationScopedApiKeysControllerTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private const string _masterPasswordHash = "master_password_hash";

    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;

    private Organization _organization = null!;
    private Guid _ownerUserId;

    public OrganizationScopedApiKeysControllerTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail, _masterPasswordHash);

        (_organization, var owner) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        _ownerUserId = owner.UserId!.Value;

        await new LoginHelper(_factory, _client).LoginAsync(ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_Owner_ReturnsClientIdAndSecret()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;
        var id = AssertHelper.AssertJsonProperty(root, "id", JsonValueKind.String).GetGuid();
        Assert.Equal("scopedApiKeyCreated", root.GetProperty("object").GetString());
        Assert.Equal($"organization.{_organization.Id}.{id}", root.GetProperty("clientId").GetString());
        Assert.Equal(30, AssertHelper.AssertJsonProperty(root, "clientSecret", JsonValueKind.String).GetString()!.Length);
        Assert.Equal("SIEM export", root.GetProperty("name").GetString());
        Assert.Equal([ApiScopes.ApiOrganizationEventsRead],
            root.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("expireAt").ValueKind);
        AssertHelper.AssertJsonProperty(root, "creationDate", JsonValueKind.String);
    }

    [Fact]
    public async Task Create_Owner_SetsNoStoreCacheControl()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Create_Owner_LogsCreatedEventForOwner()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await GetOrganizationEventsAsync();
        Assert.Contains(events, e => e.Type == EventType.Organization_ScopedApiKeyCreated &&
                                     e.ActingUserId == _ownerUserId);
    }

    [Fact]
    public async Task Create_InvalidMasterPassword_ReturnsBadRequest()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead, masterPasswordHash: "wrong");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetStoredKeysAsync());
    }

    [Theory]
    [InlineData(ApiScopes.ApiOrganization)]
    [InlineData("api.organization.policies.write")]
    public async Task Create_ScopeOutsideCatalog_ReturnsBadRequest(string scope)
    {
        var response = await CreateKeyAsync(scope);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetStoredKeysAsync());
    }

    [Fact]
    public async Task Create_OrganizationAt20Keys_ReturnsBadRequest()
    {
        var repository = _factory.GetService<IOrganizationScopedApiKeyRepository>();
        for (var i = 0; i < 20; i++)
        {
            await repository.CreateAsync(new OrganizationScopedApiKey
            {
                OrganizationId = _organization.Id,
                Name = $"Key {i}",
                ClientSecretHash = "hash",
                Scopes = $"[\"{ApiScopes.ApiOrganizationEventsRead}\"]",
            });
        }

        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(20, (await GetStoredKeysAsync()).Count);
    }

    [Fact]
    public async Task GetAll_ExistingKey_ReturnsKeyWithoutSecret()
    {
        var createResponse = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);
        using var created = await ReadJsonAsync(createResponse);
        var clientSecret = created.RootElement.GetProperty("clientSecret").GetString()!;

        var response = await _client.GetAsync($"organizations/{_organization.Id}/scoped-api-keys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(clientSecret, raw);
        using var body = JsonDocument.Parse(raw);
        var key = Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal("scopedApiKey", key.GetProperty("object").GetString());
        Assert.Equal(created.RootElement.GetProperty("clientId").GetString(), key.GetProperty("clientId").GetString());
        Assert.False(key.TryGetProperty("clientSecret", out _));
        Assert.False(key.TryGetProperty("clientSecretHash", out _));
    }

    [Fact]
    public async Task Revoke_Owner_DeletesKeyAndLogsRevokedEvent()
    {
        var id = await CreateKeyIdAsync(ApiScopes.ApiOrganizationEventsRead);

        var response = await _client.DeleteAsync($"organizations/{_organization.Id}/scoped-api-keys/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetStoredKeysAsync());
        var events = await GetOrganizationEventsAsync();
        Assert.Contains(events, e => e.Type == EventType.Organization_ScopedApiKeyRevoked &&
                                     e.ActingUserId == _ownerUserId);
    }

    [Fact]
    public async Task Revoke_KeyInAnotherOrganization_ReturnsNotFound()
    {
        var otherOwnerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherOwnerEmail);
        var (otherOrganization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: otherOwnerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        var otherKey = await _factory.GetService<IOrganizationScopedApiKeyRepository>().CreateAsync(
            new OrganizationScopedApiKey
            {
                OrganizationId = otherOrganization.Id,
                Name = "Other key",
                ClientSecretHash = "hash",
                Scopes = $"[\"{ApiScopes.ApiOrganizationEventsRead}\"]",
            });

        var response = await _client.DeleteAsync($"organizations/{_organization.Id}/scoped-api-keys/{otherKey.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(await _factory.GetService<IOrganizationScopedApiKeyRepository>().GetByIdAsync(otherKey.Id));
    }

    [Fact]
    public async Task AllEndpoints_AdminWhoIsNotOwner_ReturnsNotFound()
    {
        var id = await CreateKeyIdAsync(ApiScopes.ApiOrganizationEventsRead);
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);
        using var adminClient = _factory.CreateClient();
        await new LoginHelper(_factory, adminClient).LoginAsync(adminEmail);

        var responses = await SendToAllEndpointsAsync(adminClient, id);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r));
        Assert.Single(await GetStoredKeysAsync());
    }

    [Fact]
    public async Task AllEndpoints_LegacyOrganizationToken_ReturnsForbidden()
    {
        var id = await CreateKeyIdAsync(ApiScopes.ApiOrganizationEventsRead);
        using var organizationClient = _factory.CreateClient();
        await new LoginHelper(_factory, organizationClient).LoginWithOrganizationApiKeyAsync(_organization.Id);

        var responses = await SendToAllEndpointsAsync(organizationClient, id);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r));
    }

    [Fact]
    public async Task AllEndpoints_ScopedOrganizationToken_ReturnsForbidden()
    {
        var id = await CreateKeyIdAsync(ApiScopes.ApiOrganizationEventsRead);
        using var organizationClient = _factory.CreateClient();
        await new LoginHelper(_factory, organizationClient).LoginWithScopedOrganizationApiKeyAsync(
            _organization.Id, ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersWrite);

        var responses = await SendToAllEndpointsAsync(organizationClient, id);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r));
    }

    [Fact]
    public async Task Create_KeyCredentials_CanRequestTokenFromIdentity()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);
        using var created = await ReadJsonAsync(response);

        var context = await _factory.Identity.ContextFromOrganizationApiKeyAsync(
            created.RootElement.GetProperty("clientId").GetString()!,
            created.RootElement.GetProperty("clientSecret").GetString()!,
            scope: ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(ApiScopes.ApiOrganizationEventsRead, body.RootElement.GetProperty("scope").GetString());
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }

    /// <remarks>
    /// Identity caches clients for five minutes, so this requests a token for a client id that Identity has
    /// never looked up. A token request after revoking a cached client would succeed until the cache expires.
    /// </remarks>
    [Fact]
    public async Task Revoke_KeyCredentials_CanNoLongerRequestTokenFromIdentity()
    {
        var response = await CreateKeyAsync(ApiScopes.ApiOrganizationEventsRead);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();

        var revokeResponse = await _client.DeleteAsync($"organizations/{_organization.Id}/scoped-api-keys/{id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var context = await _factory.Identity.ContextFromOrganizationApiKeyAsync(
            created.RootElement.GetProperty("clientId").GetString()!,
            created.RootElement.GetProperty("clientSecret").GetString()!,
            scope: ApiScopes.ApiOrganizationEventsRead);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal("invalid_client", body.RootElement.GetProperty("error").GetString());
    }

    private Task<HttpResponseMessage> CreateKeyAsync(string scope, string masterPasswordHash = _masterPasswordHash) =>
        _client.PostAsJsonAsync($"organizations/{_organization.Id}/scoped-api-keys", new
        {
            masterPasswordHash,
            name = "SIEM export",
            scopes = new[] { scope },
        });

    private async Task<Guid> CreateKeyIdAsync(string scope)
    {
        var response = await CreateKeyAsync(scope);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpStatusCode[]> SendToAllEndpointsAsync(HttpClient client, Guid id)
    {
        var list = await client.GetAsync($"organizations/{_organization.Id}/scoped-api-keys");
        var create = await client.PostAsJsonAsync($"organizations/{_organization.Id}/scoped-api-keys", new
        {
            masterPasswordHash = _masterPasswordHash,
            name = "Another key",
            scopes = new[] { ApiScopes.ApiOrganizationEventsRead },
        });
        var revoke = await client.DeleteAsync($"organizations/{_organization.Id}/scoped-api-keys/{id}");
        return [list.StatusCode, create.StatusCode, revoke.StatusCode];
    }

    private Task<ICollection<OrganizationScopedApiKey>> GetStoredKeysAsync() =>
        _factory.GetService<IOrganizationScopedApiKeyRepository>().GetManyByOrganizationIdAsync(_organization.Id);

    private async Task<IEnumerable<Core.Models.Data.IEvent>> GetOrganizationEventsAsync()
    {
        var events = await _factory.GetService<IEventRepository>().GetManyByOrganizationAsync(_organization.Id,
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1), new PageOptions { PageSize = 100 });
        return events.Data;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
