using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityJwksTests
{
    private static readonly KeysRequestModel _testAccountKeys = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    [Fact]
    public async Task Jwks_WithOneIdentityCertificate_PublishesExactlyThatKey()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var factory = CreateFactory(CertificateTestConfiguration.SelfHosted(certificate));
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, certificate);
    }

    [Fact]
    public async Task Jwks_DevelopmentWithNoCertificate_PublishesOneDeveloperKey()
    {
        // Arrange
        using var factory = CreateFactory(CertificateTestConfiguration.NoCertificate());
        using var client = factory.CreateClient();

        // Act
        var keys = await JwksTestHelpers.GetPublishedKeysAsync(client);

        // Assert
        JwksTestHelpers.AssertIsOneDeveloperKey(keys);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task Token_WithOneIdentityCertificate_SignsWithThatKey(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var certificate = TestCertificates.Create();
        using var factory = CreateFactory(CertificateTestConfiguration.SelfHosted(certificate));
        await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act
        var (accessToken, _) = await factory.TokenFromPasswordAsync(requestModel.Email, requestModel.MasterPasswordHash);

        // Assert
        using var client = factory.CreateClient();
        await JwksTestHelpers.AssertSignedByPublishedKeyAsync(client, accessToken, certificate);
    }

    [Fact]
    public async Task Jwks_WithStagedCertificate_PublishesBothKeys()
    {
        // Arrange
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var factory = CreateFactory(
            CertificateTestConfiguration.SelfHosted(active, staged, futureActive: false));
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyKeyIdsAsync(client, active, staged);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task Token_AfterPromotion_UsesNewKeyId_OldKeyStillPublished(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var oldCertificate = TestCertificates.Create("CN=Bitwarden Test Old");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test New");
        using var factory = CreateFactory(
            CertificateTestConfiguration.SelfHosted(oldCertificate, newCertificate, futureActive: true, oldCertificate));
        await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act
        var (accessToken, _) = await factory.TokenFromPasswordAsync(requestModel.Email, requestModel.MasterPasswordHash);

        // Assert
        using var client = factory.CreateClient();
        await JwksTestHelpers.AssertSignedByOnePublishedKeyAsync(client, accessToken, newCertificate);
        await JwksTestHelpers.AssertPublishesExactlyKeyIdsAsync(client, newCertificate, oldCertificate);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task Token_AfterReturnFromStage6_SignsWithOutgoingKey_NewKeyStillPublished(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var outgoingCertificate = TestCertificates.Create("CN=Bitwarden Test Outgoing");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test New");
        using var factory = CreateFactory(CertificateTestConfiguration.SelfHosted(newCertificate, outgoingCertificate,
            futureActive: true, newCertificate));
        await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act
        var (accessToken, _) = await factory.TokenFromPasswordAsync(requestModel.Email, requestModel.MasterPasswordHash);

        // Assert
        using var client = factory.CreateClient();
        await JwksTestHelpers.AssertSignedByOnePublishedKeyAsync(client, accessToken, outgoingCertificate);
        await JwksTestHelpers.AssertPublishesExactlyKeyIdsAsync(client, outgoingCertificate, newCertificate);
    }

    private static IdentityApplicationFactory CreateFactory(Dictionary<string, string?> configuration)
    {
        var factory = new IdentityApplicationFactory();
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }
        return factory;
    }
}
