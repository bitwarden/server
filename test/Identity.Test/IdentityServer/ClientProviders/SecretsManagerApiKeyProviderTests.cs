using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Identity;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Identity.IdentityServer.ClientProviders;
using Duende.IdentityModel;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.ClientProviders;

public class SecretsManagerApiKeyProviderTests
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly SecretsManagerApiKeyProvider _sut;

    public SecretsManagerApiKeyProviderTests()
    {
        _apiKeyRepository = Substitute.For<IApiKeyRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();

        _sut = new SecretsManagerApiKeyProvider(_apiKeyRepository, _organizationRepository);
    }

    [Fact]
    public async Task GetAsync_ServiceAccountKey_ReturnsServiceAccountClient()
    {
        var serviceAccountId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var apiKey = SetupApiKey(serviceAccountId, organizationId);
        SetupOrganization(organizationId, useSecretsManager: true, enabled: true);

        var client = await _sut.GetAsync(apiKey.Id.ToString());

        Assert.NotNull(client);
        Assert.Equal(apiKey.Id.ToString(), client.ClientId);
        var claims = client.Claims.ToDictionary(c => c.Type, c => c.Value);
        Assert.Equal(serviceAccountId.ToString(), claims[JwtClaimTypes.Subject]);
        Assert.Equal(IdentityClientType.ServiceAccount.ToString(), claims[Claims.Type]);
        Assert.Equal(organizationId.ToString(), claims[Claims.Organization]);
    }

    [Fact]
    public async Task GetAsync_KeyWithoutServiceAccount_ReturnsNullWithoutReadingOrganization()
    {
        var apiKey = SetupApiKey(serviceAccountId: null, organizationId: Guid.Empty);

        var client = await _sut.GetAsync(apiKey.Id.ToString());

        Assert.Null(client);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Fact]
    public async Task GetAsync_OrganizationNotFound_ReturnsNull()
    {
        var organizationId = Guid.NewGuid();
        var apiKey = SetupApiKey(Guid.NewGuid(), organizationId);
        _organizationRepository.GetByIdAsync(organizationId).Returns((Organization?)null);

        var client = await _sut.GetAsync(apiKey.Id.ToString());

        Assert.Null(client);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GetAsync_OrganizationWithoutSecretsManagerOrDisabled_ReturnsNull(bool useSecretsManager,
        bool enabled)
    {
        var organizationId = Guid.NewGuid();
        var apiKey = SetupApiKey(Guid.NewGuid(), organizationId);
        SetupOrganization(organizationId, useSecretsManager, enabled);

        var client = await _sut.GetAsync(apiKey.Id.ToString());

        Assert.Null(client);
    }

    [Fact]
    public async Task GetAsync_ExpiredKey_ReturnsNull()
    {
        var organizationId = Guid.NewGuid();
        var apiKey = SetupApiKey(Guid.NewGuid(), organizationId, expireAt: DateTime.UtcNow.AddMinutes(-1));
        SetupOrganization(organizationId, useSecretsManager: true, enabled: true);

        var client = await _sut.GetAsync(apiKey.Id.ToString());

        Assert.Null(client);
    }

    private ServiceAccountApiKeyDetails SetupApiKey(Guid? serviceAccountId, Guid organizationId,
        DateTime? expireAt = null)
    {
        var apiKey = new ServiceAccountApiKeyDetails
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = serviceAccountId,
            ServiceAccountOrganizationId = organizationId,
            Name = "api-key",
            ClientSecretHash = "client-secret-hash",
            Scope = "[\"api.secrets\"]",
            EncryptedPayload = "encrypted-payload",
            Key = "key",
            ExpireAt = expireAt,
        };
        _apiKeyRepository.GetDetailsByIdAsync(apiKey.Id).Returns(apiKey);
        return apiKey;
    }

    private void SetupOrganization(Guid organizationId, bool useSecretsManager, bool enabled)
    {
        _organizationRepository.GetByIdAsync(organizationId).Returns(new Organization
        {
            Id = organizationId,
            UseSecretsManager = useSecretsManager,
            Enabled = enabled,
        });
    }
}
