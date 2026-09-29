using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

public class Saml2AcsMetadataEndpointTests(SsoApplicationFactory factory) : IClassFixture<SsoApplicationFactory>
{
    private static readonly XNamespace Md = "urn:oasis:names:tc:SAML:2.0:metadata";

    private const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    private readonly SsoApplicationFactory _factory = factory;

    [Fact]
    public async Task Metadata_ForSaml2Org_EncryptionCapableKeyDescriptor_AdvertisesBothAcceptedAlgorithmsInMgf1pFirstOrder()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = "https://idp.example.com",
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(BuildTestCertificate().RawData),
            }))
            .WithSamlSigningCertificate(BuildTestCertificate())
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = XDocument.Parse(xml);
        var encryptionAlgorithms = doc.Descendants(Md + "KeyDescriptor")
            .SelectMany(kd => kd.Elements(Md + "EncryptionMethod"))
            .Select(em => (string?)em.Attribute("Algorithm"))
            .ToList();

        Assert.Equal([RsaOaepMgf1p, RsaOaep], encryptionAlgorithms);
    }

    [Fact]
    public async Task Metadata_ForSaml2Org_AddsEncryptionMethodsToExactlyOneKeyDescriptor()
    {
        // This test app registers exactly one Service Provider (SP) certificate:
        // SamlEnvironment.SpSigningCertificate. Because of this, the Sustainsys library creates
        // exactly one KeyDescriptor element, with no "use" attribute.
        // This test confirms that OnMetadataCreated adds the algorithms to that
        // descriptor. This test also confirms that OnMetadataCreated does not add the algorithms
        // to any other element.

        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = "https://idp.example.com",
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(BuildTestCertificate().RawData),
            }))
            .WithSamlSigningCertificate(BuildTestCertificate())
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        var doc = XDocument.Parse(xml);
        var keyDescriptor = Assert.Single(doc.Descendants(Md + "KeyDescriptor"));
        Assert.Equal(2, keyDescriptor.Elements(Md + "EncryptionMethod").Count());
    }

    [Fact]
    public async Task Metadata_ForOidcOrg_ReturnsNotFound()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData { ConfigType = SsoType.OpenIdConnect }))
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Metadata_ForUnknownScheme_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/saml2/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static X509Certificate2 BuildTestCertificate()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(
                "CN=Test SP certificate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }
}
