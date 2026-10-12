using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

[SutProviderCustomize]
public class GetScopedOrganizationApiKeyClientQueryTests
{
    private static readonly DateTime _now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private const string _secretHash = "c2VjcmV0LWhhc2g=";

    [Theory, BitAutoData]
    public async Task GetAsync_ValidKey_ReturnsKeyClient(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        key.ExpireAt = _now.AddDays(1);

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.NotNull(result);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(key.Id, result.KeyId);
        Assert.Equal(_secretHash, result.ClientSecretHash);
        Assert.Equal(key.ExpireAt, result.ExpireAt);
        Assert.Equal([ApiScopes.ApiOrganizationEventsRead], result.AllowedScopes);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyWithoutExpiration_ReturnsKeyClient(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.NotNull(result);
        Assert.Null(result.ExpireAt);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyNotFound_ReturnsNull(Organization organization, Guid keyId)
    {
        var sutProvider = GetSutProvider();
        ArrangeOrganization(sutProvider, organization);

        var result = await sutProvider.Sut.GetAsync(organization.Id, keyId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ServiceAccountKey_ReturnsNull(Organization organization, Guid serviceAccountId)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        key.ServiceAccountId = serviceAccountId;

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyBelongsToAnotherOrganization_ReturnsNull(
        Organization organization, Organization otherOrganization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        ArrangeOrganization(sutProvider, otherOrganization);

        var result = await sutProvider.Sut.GetAsync(otherOrganization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyExpiresNow_ReturnsNull(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        key.ExpireAt = _now;

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyExpiresJustAfterNow_ReturnsKeyClient(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        key.ExpireAt = _now.AddTicks(1);

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.NotNull(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_OrganizationNotFound_ReturnsNull(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns((Organization?)null);

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_OrganizationDisabled_ReturnsNull(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        organization.Enabled = false;

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_OrganizationWithoutUseApi_ReturnsNull(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        organization.UseApi = false;

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_KeyWithScopesOutsideCatalog_ReturnsOnlyCatalogScopes(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var key = ArrangeValidKey(sutProvider, organization);
        key.Scope =
            $"[\"{ApiScopes.ApiOrganizationMembersRead}\",\"{ApiScopes.ApiOrganization}\",\"{ApiScopes.Api}\",\"api.organization.unknown\",\"{ApiScopes.ApiOrganizationEventsRead}\"]";

        var result = await sutProvider.Sut.GetAsync(organization.Id, key.Id);

        Assert.NotNull(result);
        Assert.Equal(
            [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead],
            result.AllowedScopes.Order());
    }

    private static SutProvider<GetScopedOrganizationApiKeyClientQuery> GetSutProvider()
    {
        var sutProvider = new SutProvider<GetScopedOrganizationApiKeyClientQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void ArrangeOrganization(
        SutProvider<GetScopedOrganizationApiKeyClientQuery> sutProvider, Organization organization)
    {
        organization.Enabled = true;
        organization.UseApi = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
    }

    private static ApiKey ArrangeValidKey(
        SutProvider<GetScopedOrganizationApiKeyClientQuery> sutProvider, Organization organization)
    {
        ArrangeOrganization(sutProvider, organization);
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Test key",
            ClientSecretHash = _secretHash,
            Scope = $"[\"{ApiScopes.ApiOrganizationEventsRead}\"]",
        };
        sutProvider.GetDependency<IApiKeyRepository>().GetByIdAsync(key.Id).Returns(key);
        return key;
    }
}
