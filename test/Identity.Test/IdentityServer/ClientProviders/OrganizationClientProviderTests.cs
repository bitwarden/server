using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;
using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class OrganizationClientProviderTests
{
    private const string SecretHash = "c2VjcmV0LWhhc2g=";

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IGetScopedOrganizationApiKeyClientQuery _getScopedOrganizationApiKeyClientQuery;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly OrganizationClientProvider _sut;

    public OrganizationClientProviderTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
        _getScopedOrganizationApiKeyClientQuery = Substitute.For<IGetScopedOrganizationApiKeyClientQuery>();
        _featureService = Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _featureService.IsEnabled(FeatureFlagKeys.ScopedOrganizationApiKeys).Returns(true);

        _sut = new OrganizationClientProvider(
            _organizationRepository,
            _organizationApiKeyRepository,
            _getScopedOrganizationApiKeyClientQuery,
            _featureService);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_LegacyIdentifier_ReturnsOrganizationApiKeyClient(Organization organization)
    {
        organization.Enabled = true;
        organization.UseApi = true;
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _organizationApiKeyRepository
            .GetManyByOrganizationIdTypeAsync(organization.Id, OrganizationApiKeyType.Default)
            .Returns([new OrganizationApiKey { OrganizationId = organization.Id, ApiKey = "legacy-key" }]);

        var client = await _sut.GetAsync(organization.Id.ToString());

        Assert.NotNull(client);
        Assert.Equal($"organization.{organization.Id}", client.ClientId);
        Assert.Equal([ApiScopes.ApiOrganization], client.AllowedScopes);
        Assert.Equal("legacy-key".Sha256(), Assert.Single(client.ClientSecrets).Value);
        Assert.True(client.Enabled);
    }

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111.22222222-2222-2222-2222-222222222222.33333333-3333-3333-3333-333333333333")]
    [InlineData("11111111-1111-1111-1111-111111111111.")]
    [InlineData(".22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111.not-a-guid")]
    [InlineData("not-a-guid.22222222-2222-2222-2222-222222222222")]
    [InlineData("{11111111-1111-1111-1111-111111111111}.22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111.22222222222222222222222222222222")]
    public async Task GetAsync_MalformedScopedIdentifier_ReturnsNull(string identifier)
    {
        var client = await _sut.GetAsync(identifier);

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_FlagOff_ReturnsNullWithoutLookingUpKey(Guid organizationId)
    {
        var key = ArrangeKey(organizationId);
        _featureService.IsEnabled(FeatureFlagKeys.ScopedOrganizationApiKeys).Returns(false);

        var client = await _sut.GetAsync($"{organizationId}.{key.KeyId}");

        Assert.Null(client);
        await _getScopedOrganizationApiKeyClientQuery.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_KeyNotUsable_ReturnsNull(Guid organizationId, Guid keyId)
    {
        var client = await _sut.GetAsync($"{organizationId}.{keyId}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_ReturnsClientForKey(Guid organizationId)
    {
        var key = ArrangeKey(organizationId);

        var client = await _sut.GetAsync($"{organizationId}.{key.KeyId}");

        Assert.NotNull(client);
        Assert.Equal($"organization.{organizationId}.{key.KeyId}", client.ClientId);
        Assert.True(client.RequireClientSecret);
        Assert.Equal(SecretHash, Assert.Single(client.ClientSecrets).Value);
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(3600, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_SecretExpiresWithKey(Guid organizationId)
    {
        var key = ArrangeKey(organizationId, expireAt: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));

        var client = await _sut.GetAsync($"{organizationId}.{key.KeyId}");

        Assert.Equal(key.ExpireAt, Assert.Single(client!.ClientSecrets).Expiration);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_AllowsKeyScopes(Guid organizationId)
    {
        var key = ArrangeKey(organizationId,
            allowedScopes: [ApiScopes.ApiOrganizationMembersRead, ApiScopes.ApiOrganizationEventsRead]);

        var client = await _sut.GetAsync($"{organizationId}.{key.KeyId}");

        Assert.Equal(key.AllowedScopes, client!.AllowedScopes);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_UsesOrganizationClaimsWithDefaultPrefix(Guid organizationId)
    {
        var key = ArrangeKey(organizationId);

        var client = await _sut.GetAsync($"{organizationId}.{key.KeyId}");

        Assert.NotNull(client);
        Assert.Equal("client_", client.ClientClaimsPrefix);
        Assert.Collection(client.Claims,
            c =>
            {
                Assert.Equal(JwtClaimTypes.Subject, c.Type);
                Assert.Equal(organizationId.ToString(), c.Value);
            },
            c =>
            {
                Assert.Equal(Claims.Type, c.Type);
                Assert.Equal(IdentityClientType.Organization.ToString(), c.Value);
            });
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_UppercaseIdentifier_ReturnsCanonicalClientId(Guid organizationId)
    {
        var key = ArrangeKey(organizationId);

        var client = await _sut.GetAsync(
            $"{organizationId.ToString().ToUpperInvariant()}.{key.KeyId.ToString().ToUpperInvariant()}");

        Assert.NotNull(client);
        Assert.Equal($"organization.{organizationId}.{key.KeyId}", client.ClientId);
    }

    private ScopedOrganizationApiKeyClient ArrangeKey(Guid organizationId, DateTime? expireAt = null,
        IReadOnlyCollection<string> allowedScopes = null)
    {
        var key = new ScopedOrganizationApiKeyClient(
            organizationId,
            Guid.NewGuid(),
            SecretHash,
            expireAt,
            allowedScopes ?? [ApiScopes.ApiOrganizationEventsRead]);
        _getScopedOrganizationApiKeyClientQuery.GetAsync(organizationId, key.KeyId).Returns(key);
        return key;
    }
}
