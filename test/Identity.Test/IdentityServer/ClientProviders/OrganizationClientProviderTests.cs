using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
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
    private readonly IOrganizationScopedApiKeyRepository _organizationScopedApiKeyRepository;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly OrganizationClientProvider _sut;

    public OrganizationClientProviderTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
        _organizationScopedApiKeyRepository = Substitute.For<IOrganizationScopedApiKeyRepository>();
        _featureService = Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _featureService.IsEnabled(FeatureFlagKeys.ScopedOrganizationApiKeys).Returns(true);

        _sut = new OrganizationClientProvider(
            _organizationRepository,
            _organizationApiKeyRepository,
            _organizationScopedApiKeyRepository,
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
    public async Task GetAsync_ScopedIdentifier_FlagOff_ReturnsNull(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        _featureService.IsEnabled(FeatureFlagKeys.ScopedOrganizationApiKeys).Returns(false);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_KeyNotFound_ReturnsNull(Organization organization, Guid keyId)
    {
        organization.Enabled = true;
        organization.UseApi = true;
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        var client = await _sut.GetAsync($"{organization.Id}.{keyId}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_KeyBelongsToAnotherOrganization_ReturnsNull(
        Organization organization, Organization otherOrganization)
    {
        var key = ArrangeValidScopedKey(organization);
        otherOrganization.Enabled = true;
        otherOrganization.UseApi = true;
        _organizationRepository.GetByIdAsync(otherOrganization.Id).Returns(otherOrganization);

        var client = await _sut.GetAsync($"{otherOrganization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_KeyExpired_ReturnsNull(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        key.ExpireAt = DateTime.UtcNow.AddMinutes(-1);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_KeyExpiresInFuture_ReturnsClient(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        key.ExpireAt = DateTime.UtcNow.AddDays(1);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.NotNull(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_SecretExpiresWithKey(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        key.ExpireAt = DateTime.UtcNow.AddDays(1);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Equal(key.ExpireAt, Assert.Single(client!.ClientSecrets).Expiration);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_OrganizationNotFound_ReturnsNull(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        _organizationRepository.GetByIdAsync(organization.Id).Returns((Organization)null);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_OrganizationDisabled_ReturnsNull(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        organization.Enabled = false;

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_OrganizationWithoutUseApi_ReturnsNull(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        organization.UseApi = false;

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.Null(client);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_ReturnsClientForKey(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.NotNull(client);
        Assert.Equal($"organization.{organization.Id}.{key.Id}", client.ClientId);
        Assert.True(client.RequireClientSecret);
        Assert.Equal(SecretHash, Assert.Single(client.ClientSecrets).Value);
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(3600, client.AccessTokenLifetime);
        Assert.True(client.Enabled);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_UsesOrganizationClaimsWithDefaultPrefix(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.NotNull(client);
        Assert.Equal("client_", client.ClientClaimsPrefix);
        Assert.Collection(client.Claims,
            c =>
            {
                Assert.Equal(JwtClaimTypes.Subject, c.Type);
                Assert.Equal(organization.Id.ToString(), c.Value);
            },
            c =>
            {
                Assert.Equal(Claims.Type, c.Type);
                Assert.Equal(IdentityClientType.Organization.ToString(), c.Value);
            });
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_UppercaseIdentifier_ReturnsCanonicalClientId(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);

        var client = await _sut.GetAsync(
            $"{organization.Id.ToString().ToUpperInvariant()}.{key.Id.ToString().ToUpperInvariant()}");

        Assert.NotNull(client);
        Assert.Equal($"organization.{organization.Id}.{key.Id}", client.ClientId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ScopedIdentifier_AllowsOnlyCatalogScopesFromKey(Organization organization)
    {
        var key = ArrangeValidScopedKey(organization);
        key.Scopes =
            $"[\"{ApiScopes.ApiOrganizationMembersRead}\",\"{ApiScopes.ApiOrganization}\",\"{ApiScopes.Api}\",\"api.organization.unknown\",\"{ApiScopes.ApiOrganizationEventsRead}\"]";

        var client = await _sut.GetAsync($"{organization.Id}.{key.Id}");

        Assert.NotNull(client);
        Assert.Equal(
            [ApiScopes.ApiOrganizationEventsRead, ApiScopes.ApiOrganizationMembersRead],
            client.AllowedScopes.Order());
    }

    private OrganizationScopedApiKey ArrangeValidScopedKey(Organization organization)
    {
        organization.Enabled = true;
        organization.UseApi = true;
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        var key = new OrganizationScopedApiKey
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Test key",
            ClientSecretHash = SecretHash,
            Scopes = $"[\"{ApiScopes.ApiOrganizationEventsRead}\"]",
        };
        _organizationScopedApiKeyRepository.GetByIdAsync(key.Id).Returns(key);
        return key;
    }
}
