using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Bit.IntegrationTestCommon;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Sso.IntegrationTest.Utilities.SsoCertificateTestHelpers;

namespace Bit.Sso.IntegrationTest.Certificates;

public class SsoAssertionDecryptionTests
{
    [Fact]
    public async Task Acs_AssertionEncryptedToTheIdentityCertificate_Decrypts()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLoginSteps.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLoginSteps.BuildSignedAssertion(login, idpCertificate), certificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLoginSteps.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToAnotherCertificate_Fails()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var otherCertificate = TestCertificates.Create("CN=Another Certificate");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLoginSteps.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLoginSteps.BuildSignedAssertion(login, idpCertificate), otherCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLoginSteps.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Encrypted Assertion(s) could not be decrypted using the configured Service Certificate(s).",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToActiveSsoEncryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, encryption);

        // Assert
        Assert.Equal([Key("encryption", encryption)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToStagedEncryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var stagedEncryption = TestCertificates.Create("CN=Bitwarden Test SSO Staged Encryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoEncryptionFutureCertificate(stagedEncryption, active: false));

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, stagedEncryption);

        // Assert
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_FallbackAfterIdentityStage5Rollback_AssertionEncryptedToIdentityStagedCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityNew = TestCertificates.Create("CN=Bitwarden Test Identity New");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate,
            CertificateTestConfiguration.SelfHosted(identity, identityNew, futureActive: false));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, identityNew);

        // Assert
        Assert.Equal([Key(null, identity)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToDecryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test SSO Decryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoDecryptionCertificates(decryption));

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, decryption);

        // Assert
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AfterReturnFromStage6_AssertionEncryptedToNewCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var outgoing = TestCertificates.Create("CN=Bitwarden Test SSO Encryption Outgoing");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test SSO Encryption New");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(newCertificate)
            .WithSsoEncryptionFutureCertificate(outgoing, active: true)
            .WithSsoDecryptionCertificates(newCertificate));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, newCertificate);

        // Assert
        Assert.Equal([Key("encryption", outgoing)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLoginSteps.AssertSucceededAsync(response);
    }

    private static Task<Saml2AcsArrangement> ArrangeAcsAsync(X509Certificate2 idpCertificate,
        Dictionary<string, string?> configuration) =>
        Saml2AcsPostHarness.ArrangeAsync(string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: configuration);

    private static async Task<HttpResponseMessage> PostEncryptedAssertionAsync(Saml2AcsArrangement arrangement,
        X509Certificate2 idpCertificate, X509Certificate2 encryptionCertificate)
    {
        var login = await Saml2SpInitiatedLoginSteps.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLoginSteps.BuildSignedAssertion(login, idpCertificate), encryptionCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);
        return await Saml2SpInitiatedLoginSteps.PostAcsAsync(arrangement, login, responseXml);
    }
}
