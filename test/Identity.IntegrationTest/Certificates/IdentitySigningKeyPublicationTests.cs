using Bit.Identity.IntegrationTest.Helpers;
using Bit.IntegrationTestCommon;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.Certificates;

public class IdentitySigningKeyPublicationTests
{
    [Fact]
    public async Task Jwks_WithOneIdentityCertificate_PublishesExactlyThatKey()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.SelfHosted(certificate));
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, certificate);
    }

    [Fact]
    public async Task Jwks_DevelopmentWithNoCertificate_PublishesOneDeveloperKey()
    {
        // Arrange
        using var factory = CertificateTestHelpers.CreateFactory(CertificateTestConfiguration.NoCertificate());
        using var client = factory.CreateClient();

        // Act
        var keys = await JwksTestHelpers.GetPublishedKeysAsync(client);

        // Assert
        JwksTestHelpers.AssertIsOneDeveloperKey(keys);
    }

    [Fact]
    public async Task Jwks_WithStagedCertificate_PublishesBothKeys()
    {
        // Arrange
        using var active = TestCertificates.Create("CN=Bitwarden Test Active");
        using var staged = TestCertificates.Create("CN=Bitwarden Test Staged");
        using var factory = CertificateTestHelpers.CreateFactory(
            CertificateTestConfiguration.SelfHosted(active, staged, futureActive: false));
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyKeyIdsAsync(client, active, staged);
    }
}
