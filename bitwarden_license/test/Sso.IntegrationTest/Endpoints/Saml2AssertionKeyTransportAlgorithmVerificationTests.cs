using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
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
    private const string MeterName = "Bitwarden.Sso.Saml2";
    private const string InstrumentName = "bitwarden.sso.saml2.unsupported_key_transport_algorithm";

    [Fact]
    public async Task CouldHandleAsync_WithUnacceptedKeyTransportAlgorithm_RecordsUnsupportedSamlKeyEncryptionMeasurement()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(BuildEncryptedAssertion(Rsa15));
        var (samlOptions, organizationId, context, collector) = arrangement;

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert: the measurement carries the algorithm only. It never carries the organization ID.
        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(Rsa15, measurement.Tags["algorithm"]);
        Assert.DoesNotContain(measurement.Tags, tag => Equals(tag.Value, organizationId.ToString()));
    }

    [Fact]
    public async Task CouldHandleAsync_WithAcceptedKeyTransportAlgorithm_RecordsNoMeasurement()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(BuildEncryptedAssertion(RsaOaepMgf1p));
        var (samlOptions, organizationId, context, collector) = arrangement;

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_WithNoEncryptedAssertions_RecordsNoMeasurement()
    {
        // Arrange
        using var arrangement = await ArrangeAsync(
            "<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");
        var (samlOptions, organizationId, context, collector) = arrangement;

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_WithMismatchedIssuer_DoesNotReachVerification_RecordsNoMeasurement()
    {
        // Arrange: The issuer does not match the seeded IdpEntityId value. The entity-ID guard
        // in CouldHandleAsync must reject the request before the key transport algorithm verification logic runs.
        using var arrangement = await ArrangeAsync(
            BuildEncryptedAssertion(Rsa15), issuer: "https://not-the-configured-idp.example.com");
        var (samlOptions, organizationId, context, collector) = arrangement;

        // Act
        await samlOptions.CouldHandleAsync(organizationId.ToString(), context);

        // Assert
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    private static async Task<Arrangement> ArrangeAsync(
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
            .BuildAsync();

        var organizationId = testData.Organization!.Id;
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId.ToString());
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        var collector = new MetricCollector<long>(
            testData.Factory.Services.GetRequiredService<IMeterFactory>(), MeterName, InstrumentName);

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

        return new Arrangement(samlOptions, organizationId, context, collector);
    }

    // Disposing this disposes the collector's underlying listener, so a test does not leak it.
    private sealed record Arrangement(
        Saml2Options SamlOptions, Guid OrganizationId, HttpContext Context, MetricCollector<long> Collector) : IDisposable
    {
        public void Dispose() => Collector.Dispose();
    }

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
