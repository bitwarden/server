using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bit.Sso.IntegrationTest.Utilities;

/// <summary>
/// Builds a real, database-backed SAML organization and posts forged responses to its assertion
/// consumer service (ACS) URL through the whole request pipeline.
/// </summary>
public static class Saml2AcsPostHarness
{
    public const string IdpEntityId = "https://idp.example.com";
    public const string Rsa15 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    public const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";

    /// <summary>
    /// Seeds an organization with a SAML configuration, and builds a client that does not follow
    /// redirects. The arrangement also holds a response that carries the given assertion element.
    /// </summary>
    public static async Task<Saml2AcsArrangement> ArrangeAsync(
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

        // The request pipeline finds the organization scheme only after the scheme provider has
        // loaded it by name. A request with no such earlier call gets no request handler schemes.
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId.ToString());
        Assert.IsType<DynamicAuthenticationScheme>(scheme);

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        return new Saml2AcsArrangement(
            testData.Factory, organizationId, client, BuildResponseXml(assertionElement, issuer));
    }

    /// <summary>
    /// Posts the Base64 encoded response in the <c>SAMLResponse</c> form field to the ACS URL of the organization.
    /// </summary>
    public static Task<HttpResponseMessage> PostAcsAsync(
        HttpClient client, Guid organizationId, string responseXml)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });

        return client.PostAsync(SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId.ToString()), form);
    }

    public static string BuildResponseXml(string assertionElement, string issuer = IdpEntityId) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{issuer}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

    public static X509Certificate2 BuildIdpCertificate()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(
                "CN=Test IdP signing certificate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    public static string BuildEncryptedAssertion(string algorithm) =>
        "<saml:EncryptedAssertion>" +
        "<xenc:EncryptedKey>" +
        $"<xenc:EncryptionMethod Algorithm=\"{algorithm}\" />" +
        "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedKey>" +
        "<xenc:EncryptedData>" +
        "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedData>" +
        "</saml:EncryptedAssertion>";

    public static string BuildSubjectAssertion(string nameIdEmail) =>
        "<saml:Assertion ID=\"_subject_assertion\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        "<saml:Issuer>idp</saml:Issuer>" +
        "<saml:Subject>" +
        "<saml:NameID Format=\"urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress\">" +
        $"{nameIdEmail}</saml:NameID>" +
        "</saml:Subject>" +
        "</saml:Assertion>";
}

/// <summary>
/// The host and the client that <see cref="Saml2AcsPostHarness.ArrangeAsync"/> builds.
/// Disposing this disposes the client and the host.
/// </summary>
public sealed record Saml2AcsArrangement(
    SsoApplicationFactory Factory, Guid OrganizationId, HttpClient Client, string ResponseXml) : IDisposable
{
    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}
