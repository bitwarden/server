using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.Certificates;

public class IdentityTokenSigningTests
{
    private static readonly KeysRequestModel _testAccountKeys = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task Token_WithOneIdentityCertificate_SignsWithThatKey(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var certificate = TestCertificates.Create();
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.SelfHosted(certificate));
        await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act
        var (accessToken, _) = await factory.TokenFromPasswordAsync(requestModel.Email, requestModel.MasterPasswordHash);

        // Assert
        using var client = factory.CreateClient();
        await JwksTestHelpers.AssertSignedByPublishedKeyAsync(client, accessToken, certificate);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task Token_AfterPromotion_UsesNewKeyId_OldKeyStillPublished(RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var oldCertificate = TestCertificates.Create("CN=Bitwarden Test Old");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test New");
        using var factory = CertificateTestHelpers.CreateFactory(
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
    public async Task Token_AfterCleanUpRollback_SignsWithOutgoingKey_NewKeyStillPublished(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = _testAccountKeys;
        using var outgoingCertificate = TestCertificates.Create("CN=Bitwarden Test Outgoing");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test New");
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.SelfHosted(newCertificate, outgoingCertificate,
            futureActive: true, newCertificate));
        await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act
        var (accessToken, _) = await factory.TokenFromPasswordAsync(requestModel.Email, requestModel.MasterPasswordHash);

        // Assert
        using var client = factory.CreateClient();
        await JwksTestHelpers.AssertSignedByOnePublishedKeyAsync(client, accessToken, outgoingCertificate);
        await JwksTestHelpers.AssertPublishesExactlyKeyIdsAsync(client, outgoingCertificate, newCertificate);
    }
}
