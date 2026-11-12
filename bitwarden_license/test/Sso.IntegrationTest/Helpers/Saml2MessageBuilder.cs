using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2;

namespace Bit.Sso.IntegrationTest.Helpers;

/// <summary>
/// Builds SAML messages, signing certificates, and the <see cref="HttpContext"/> that carries a message
/// to a SAML endpoint, for tests that call <c>Saml2Options.CouldHandleAsync</c> directly.
/// </summary>
public static class Saml2MessageBuilder
{
    public const string IdpEntityId = "https://idp.example.com";

    public static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    // Callers that place two plaintext assertions in one envelope must give each a distinct ID,
    // so signature references resolve to the intended element.
    public static XmlDocument BuildAssertionDocument(string assertionId = "_assertion") =>
        XmlHelpers.XmlDocumentFromString(
            $"<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"{assertionId}\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "</saml:Assertion>");

    public static XmlElement BuildSignedAssertion(X509Certificate2 signingCertificate,
        string assertionId = "_assertion")
    {
        var document = BuildAssertionDocument(assertionId);
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!;
    }

    public static string BuildResponseXml(string assertionElement, string? issuer = null) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{issuer ?? IdpEntityId}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

    public static HttpContext BuildPostContext(SsoTestData testData, string path, string formField,
        string messageXml)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("sso.example.com");
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [formField] = Convert.ToBase64String(Encoding.UTF8.GetBytes(messageXml)),
        });
        return context;
    }
}
