using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using Bit.Core.Auth.Enums;
using Bit.IntegrationTestCommon;
using Bit.Sso.IntegrationTest.Helpers;
using Bit.Test.Common.Helpers;
using Sustainsys.Saml2;
using Xunit;
using static Bit.Sso.IntegrationTest.Helpers.SsoCertificateTestHelpers;

namespace Bit.Sso.IntegrationTest.Certificates;

public class SsoMetadataCertificateTests
{
    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_HasOneKeyDescriptorWithNoUse()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var keyDescriptor = Assert.Single(XDocument.Parse(xml).Descendants(MetadataNamespace + "KeyDescriptor"));
        Assert.Null(keyDescriptor.Attribute("use"));
        Assert.Equal(
            Convert.ToBase64String(certificate.RawData),
            keyDescriptor.Descendants(XmlDsigNamespace + "X509Certificate").Single().Value.Trim());
    }

    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(IsSignedBy(XmlHelpers.XmlDocumentFromString(xml).DocumentElement!, certificate));
    }

    [Fact]
    public async Task AuthnRequest_WithSigningAlwaysAndOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, selfHosted: true, spSigningBehavior: Saml2SigningBehavior.Always,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));

        // Act
        var login = await Saml2SpInitiatedLoginSteps.StartAsync(arrangement);

        // Assert
        Assert.True(IsSignedBy(login.AuthnRequest, certificate));
    }

    [Fact]
    public async Task Metadata_DevelopmentWithNoCertificate_HasNoKeyDescriptor()
    {
        // Arrange
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = XDocument.Parse(xml);
        Assert.Single(document.Descendants(MetadataNamespace + "SPSSODescriptor"));
        Assert.Empty(document.Descendants(MetadataNamespace + "KeyDescriptor"));
    }

    [Fact]
    public async Task Metadata_SsoRolesSetToTheIdentityCertificate_OneKeyDescriptorWithNoUse()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var configuration = CertificateTestConfiguration.SelfHosted(certificate)
            .WithSsoSigningCertificate(certificate)
            .WithSsoEncryptionCertificate(certificate);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key(null, certificate)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_SplitCertificates_SigningAndEncryptionKeyDescriptors()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key("signing", signing), Key("encryption", encryption)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_StagedSigning_PublishedAsSecondSigningKey_MetadataSignedByActive()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var stagedSigning = TestCertificates.Create("CN=Bitwarden Test SSO Staged Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoStagedSigningCertificate(stagedSigning)
            .WithSsoEncryptionCertificate(encryption);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal(
            [Key("signing", signing), Key("encryption", encryption), Key("signing", stagedSigning)],
            KeyDescriptors(xml));
        var document = XmlHelpers.XmlDocumentFromString(xml).DocumentElement!;
        Assert.True(IsSignedBy(document, signing));
        Assert.False(IsSignedBy(document, stagedSigning));
    }

    [Fact]
    public async Task Metadata_StagedEncryption_NotPublished()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var stagedEncryption = TestCertificates.Create("CN=Bitwarden Test SSO Staged Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoEncryptionFutureCertificate(stagedEncryption, active: false);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key("signing", signing), Key("encryption", encryption)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_FallbackWithIdentityAndSsoStaged_BothPublished_ActiveSigns()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        var configuration = CertificateTestConfiguration.SelfHosted(identity, identityStaged, futureActive: false)
            .WithSsoStagedSigningCertificate(ssoStaged);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal(
            [Key(null, identity), Key("signing", identityStaged), Key("signing", ssoStaged)],
            KeyDescriptors(xml));
        var document = XmlHelpers.XmlDocumentFromString(xml).DocumentElement!;
        Assert.True(IsSignedBy(document, identity));
        Assert.False(IsSignedBy(document, identityStaged));
        Assert.False(IsSignedBy(document, ssoStaged));
    }

    private static bool IsSignedBy(XmlElement element, X509Certificate2 certificate)
    {
        var signature = Assert.Single(element.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)
            .Cast<XmlElement>());
        var signedXml = new SignedXml(element);
        signedXml.LoadXml(signature);
        return signedXml.CheckSignature(certificate, verifySignatureOnly: true);
    }
}
