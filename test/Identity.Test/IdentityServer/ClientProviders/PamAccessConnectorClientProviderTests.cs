using Bit.Core.Auth.Identity;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class PamAccessConnectorClientProviderTests
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamAccessConnectorRepository _pamAccessConnectorRepository;
    private readonly PamAccessConnectorClientProvider _sut;

    public PamAccessConnectorClientProviderTests()
    {
        _apiKeyRepository = Substitute.For<IApiKeyRepository>();
        _pamAccessConnectorRepository = Substitute.For<IPamAccessConnectorRepository>();

        _sut = new PamAccessConnectorClientProvider(_apiKeyRepository, _pamAccessConnectorRepository);
    }

    [Fact]
    public async Task GetAsync_NonGuidIdentifier_ReturnsNull()
    {
        var client = await _sut.GetAsync("non-guid");

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_ApiKeyMissing_ReturnsNull()
    {
        var client = await _sut.GetAsync(Guid.NewGuid().ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_ApiKeyExpired_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId)
            .Returns(CreateApiKey(apiKeyId, expireAt: DateTime.UtcNow.AddMinutes(-1)));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_NoAccessConnectorForApiKey_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId).Returns((PamAccessConnectorDetails?)null);

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_AccessConnectorDisabled_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateAccessConnectorDetails(apiKeyId, status: PamAccessConnectorStatus.Disabled));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_OrganizationDisabled_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateAccessConnectorDetails(apiKeyId, organizationEnabled: false));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_OrganizationUsePamFalse_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateAccessConnectorDetails(apiKeyId, organizationUsePam: false));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_EnabledAccessConnectorLicensedOrg_ReturnsClientCredentialsClient()
    {
        var apiKeyId = Guid.NewGuid();
        var apiKey = CreateApiKey(apiKeyId);
        var accessConnectorDetails = CreateAccessConnectorDetails(apiKeyId);
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(apiKey);
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId).Returns(accessConnectorDetails);

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.NotNull(client);
        Assert.Equal($"access-connector.{apiKeyId}", client.ClientId);
        Assert.True(client.RequireClientSecret);
        Assert.Single(client.ClientSecrets);
        var scope = Assert.Single(client.AllowedScopes);
        Assert.Equal(ApiScopes.ApiPamRotation, scope);
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, client.AccessTokenLifetime);
        Assert.Null(client.ClientClaimsPrefix);
        Assert.Equal("encrypted-payload", client.Properties["encryptedPayload"]);
        Assert.Contains(client.Claims, c =>
            c.Type == JwtClaimTypes.Subject && c.Value == accessConnectorDetails.Id.ToString());
        Assert.Contains(client.Claims, c =>
            c.Type == Claims.Type && c.Value == IdentityClientType.AccessConnector.ToString());
        Assert.Contains(client.Claims, c =>
            c.Type == Claims.Organization && c.Value == accessConnectorDetails.OrganizationId.ToString());
    }

    [Fact]
    public async Task GetAsync_ApiKeyNeverExpires_NullExpireAt_ReturnsClient()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId, expireAt: null));
        _pamAccessConnectorRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateAccessConnectorDetails(apiKeyId));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        // Access connector credentials are long-lived, so a null ExpireAt never expires.
        Assert.NotNull(client);
    }

    private static ApiKey CreateApiKey(Guid apiKeyId, DateTime? expireAt = null) => new()
    {
        Id = apiKeyId,
        ServiceAccountId = null,
        Name = "access-connector-credential",
        ClientSecretHash = "hashed-secret",
        Scope = $"[\"{ApiScopes.ApiPamRotation}\"]",
        EncryptedPayload = "encrypted-payload",
        Key = "2.key|data|mac",
        ExpireAt = expireAt,
    };

    private static PamAccessConnectorDetails CreateAccessConnectorDetails(
        Guid apiKeyId,
        PamAccessConnectorStatus status = PamAccessConnectorStatus.Enabled,
        bool organizationEnabled = true,
        bool organizationUsePam = true) => PamAccessConnectorDetails.From(
            new PamAccessConnector
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Name = "access-connector-1",
                ApiKeyId = apiKeyId,
                Status = status,
            },
            organizationEnabled,
            organizationUsePam);
}
