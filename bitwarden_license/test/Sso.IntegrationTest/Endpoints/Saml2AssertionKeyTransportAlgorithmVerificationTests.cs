using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2.AspNetCore2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Uses a <c>Saml2Options</c> object.
/// Does not build the <c>Saml2Options</c> object by hand. Proves that
/// the check for the encrypted-assertion key-transport algorithm is practically reachable.
/// </summary>
public class Saml2AssertionKeyTransportAlgorithmVerificationTests
{
    private const string IdpEntityId = "https://idp.example.com";
    private const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string Rsa15 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";

    [Fact]
    public async Task CouldHandleAsync_WithUnacceptedKeyTransportAlgorithm_LogsUnsupportedSamlKeyEncryption()
    {
        // Arrange
        var (samlOptions, organizationId, context) = await ArrangeAsync(BuildEncryptedAssertion(Rsa15));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        var record = Assert.Single(GetCensusRecords(context));
        Assert.Equal(organizationId.ToString(), GetStructuredValue(record, "Scheme"));
        Assert.Equal(Rsa15, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public async Task CouldHandleAsync_WithAcceptedKeyTransportAlgorithm_LogsNothing()
    {
        // Arrange
        var (samlOptions, organizationId, context) = await ArrangeAsync(BuildEncryptedAssertion(RsaOaepMgf1p));

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(GetCensusRecords(context));
    }

    [Fact]
    public async Task CouldHandleAsync_WithNoEncryptedAssertions_LogsNothing()
    {
        // Arrange
        var (samlOptions, organizationId, context) = await ArrangeAsync(
            "<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(GetCensusRecords(context));
    }

    [Fact]
    public async Task CouldHandleAsync_WithMismatchedIssuer_DoesNotReachCensus_LogsNothing()
    {
        // Arrange: The issuer does not match the seeded IdpEntityId value. The entity-ID guard
        // in CouldHandleAsync must reject the request before the key transport algorithm verification logic runs.
        var (samlOptions, organizationId, context) = await ArrangeAsync(
            BuildEncryptedAssertion(Rsa15), issuer: "https://not-the-configured-idp.example.com");

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(GetCensusRecords(context));
    }

    private static async Task<(Saml2Options SamlOptions, Guid OrganizationId, HttpContext Context)> ArrangeAsync(
        string assertionElement, string issuer = IdpEntityId)
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = IdpEntityId,
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(BuildIdpCertificate().RawData),
            }))
            .WithFakeLogging()
            .BuildAsync();

        var organizationId = testData.Organization!.Id;
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId.ToString());
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        var responseXml = BuildResponseXml(assertionElement, issuer);
        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Path = SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId.ToString());
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });

        return (samlOptions, organizationId, context);
    }

    private static IReadOnlyList<FakeLogRecord> GetCensusRecords(HttpContext context) =>
        context.RequestServices.GetRequiredService<FakeLogCollector>()
            .GetSnapshot()
            .Where(r => r.StructuredState!.Any(entry => entry.Key == "KeyEncryptionAlgorithm"))
            .ToList();

    private static string? GetStructuredValue(FakeLogRecord record, string key) =>
        Assert.Single(record.StructuredState!, entry => entry.Key == key).Value;

    private static string BuildResponseXml(string assertionElement, string issuer) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{issuer}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

    private static X509Certificate2 BuildIdpCertificate()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(
                "CN=Test IdP signing certificate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    private static string BuildEncryptedAssertion(string algorithm) =>
        "<saml:EncryptedAssertion>" +
        "<xenc:EncryptedKey>" +
        $"<xenc:EncryptionMethod Algorithm=\"{algorithm}\" />" +
        "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedKey>" +
        "<xenc:EncryptedData>" +
        "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedData>" +
        "</saml:EncryptedAssertion>";
}
