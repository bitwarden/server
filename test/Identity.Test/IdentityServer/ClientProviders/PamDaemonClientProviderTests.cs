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

public class PamDaemonClientProviderTests
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IPamDaemonRepository _pamDaemonRepository;
    private readonly PamDaemonClientProvider _sut;

    public PamDaemonClientProviderTests()
    {
        _apiKeyRepository = Substitute.For<IApiKeyRepository>();
        _pamDaemonRepository = Substitute.For<IPamDaemonRepository>();

        _sut = new PamDaemonClientProvider(_apiKeyRepository, _pamDaemonRepository);
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
    public async Task GetAsync_NoDaemonForApiKey_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId).Returns((PamDaemonDetails?)null);

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_DaemonRevoked_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateDaemonDetails(apiKeyId, status: PamDaemonStatus.Revoked));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_OrganizationDisabled_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateDaemonDetails(apiKeyId, organizationEnabled: false));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_OrganizationUsePamFalse_ReturnsNull()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId));
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId)
            .Returns(CreateDaemonDetails(apiKeyId, organizationUsePam: false));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_EnrolledDaemonLicensedOrg_ReturnsClientCredentialsClient()
    {
        var apiKeyId = Guid.NewGuid();
        var apiKey = CreateApiKey(apiKeyId);
        var daemonDetails = CreateDaemonDetails(apiKeyId);
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(apiKey);
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId).Returns(daemonDetails);

        var client = await _sut.GetAsync(apiKeyId.ToString());

        Assert.NotNull(client);
        Assert.Equal($"daemon.{apiKeyId}", client.ClientId);
        Assert.True(client.RequireClientSecret);
        // The usage of this secret is tested in integration tests
        Assert.Single(client.ClientSecrets);
        var scope = Assert.Single(client.AllowedScopes);
        Assert.Equal(ApiScopes.ApiPamRotation, scope);
        Assert.Equal(GrantTypes.ClientCredentials, client.AllowedGrantTypes);
        Assert.Null(client.ClientClaimsPrefix);
        Assert.Equal("encrypted-payload", client.Properties["encryptedPayload"]);
        Assert.Contains(client.Claims, c =>
            c.Type == JwtClaimTypes.Subject && c.Value == daemonDetails.Id.ToString());
        Assert.Contains(client.Claims, c =>
            c.Type == Claims.Type && c.Value == IdentityClientType.RotationDaemon.ToString());
        Assert.Contains(client.Claims, c =>
            c.Type == Claims.Organization && c.Value == daemonDetails.OrganizationId.ToString());
    }

    [Fact]
    public async Task GetAsync_ApiKeyNeverExpires_NullExpireAt_ReturnsClient()
    {
        var apiKeyId = Guid.NewGuid();
        _apiKeyRepository.GetByIdAsync(apiKeyId).Returns(CreateApiKey(apiKeyId, expireAt: null));
        _pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKeyId).Returns(CreateDaemonDetails(apiKeyId));

        var client = await _sut.GetAsync(apiKeyId.ToString());

        // Daemon credentials are long-lived: a null ExpireAt must not be treated as expired.
        Assert.NotNull(client);
    }

    private static ApiKey CreateApiKey(Guid apiKeyId, DateTime? expireAt = null) => new()
    {
        Id = apiKeyId,
        ServiceAccountId = null,
        Name = "daemon-credential",
        ClientSecretHash = "hashed-secret",
        Scope = $"[\"{ApiScopes.ApiPamRotation}\"]",
        EncryptedPayload = "encrypted-payload",
        Key = "2.key|data|mac",
        ExpireAt = expireAt,
    };

    private static PamDaemonDetails CreateDaemonDetails(
        Guid apiKeyId,
        PamDaemonStatus status = PamDaemonStatus.Enrolled,
        bool organizationEnabled = true,
        bool organizationUsePam = true) => PamDaemonDetails.From(
            new PamDaemon
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Name = "daemon-1",
                ApiKeyId = apiKeyId,
                Status = status,
            },
            organizationEnabled,
            organizationUsePam);
}
