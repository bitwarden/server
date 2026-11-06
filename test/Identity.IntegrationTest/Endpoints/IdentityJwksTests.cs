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
