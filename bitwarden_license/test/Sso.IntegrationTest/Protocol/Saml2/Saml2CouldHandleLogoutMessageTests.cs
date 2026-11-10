using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities.Saml2;
using Sustainsys.Saml2;
using Xunit;
using static Bit.Sso.IntegrationTest.Utilities.Saml2CouldHandleTestHelpers;
using static Bit.Sso.IntegrationTest.Utilities.Saml2MessageBuilder;

namespace Bit.Sso.IntegrationTest.Protocol.Saml2;

/// <summary>
/// Exercises <c>CouldHandleAsync</c> with SAML logout messages. A <c>LogoutRequest</c> or
/// <c>LogoutResponse</c> carries no assertions, so the assertion signature check lets it through on
/// both the HTTP-POST and the HTTP-Redirect bindings.
/// </summary>
public class Saml2CouldHandleLogoutMessageTests
{
    [Fact]
    public async Task CouldHandleAsync_SignedLogoutRequestAndWantAssertionsSigned_DoesNotThrow()
    {
        // An identity-provider-initiated <LogoutRequest> carries no assertions by definition.
        // Sustainsys.Saml2 mounts the single logout endpoint under the same module path as the
        // assertion consumer service, so CouldHandleAsync inspects logout messages too. 
        // WantAssertionsSigned applies only to a <Response>, so a logout message passes
        // through untouched.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var logoutRequest = BuildSignedLogoutRequest(idpCertificate);

        var arrangement = await ArrangeLogoutAsync(
            logoutRequest, idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_SignedLogoutRequestAndWantAssertionsSignedFalse_DoesNotThrow()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var logoutRequest = BuildSignedLogoutRequest(idpCertificate);

        var arrangement = await ArrangeLogoutAsync(
            logoutRequest, idpCertificate, spCertificate, wantAssertionsSigned: false);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_SignedLogoutResponseAndWantAssertionsSigned_DoesNotThrow()
    {
        // A service-provider-initiated logout ends with the identity provider posting a
        // <LogoutResponse> in the SAMLResponse form field. The message carries no assertions, so
        // the WantAssertionsSigned must not operate on it. 
        // The SAMLResponse field name and message type must both be checked.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var logoutResponse = BuildSignedLogoutResponse(idpCertificate);

        var arrangement = await ArrangeLogoutAsync(
            logoutResponse, idpCertificate, spCertificate, wantAssertionsSigned: true,
            formField: "SAMLResponse");

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_RedirectBindingLogoutRequestAndWantAssertionsSigned_DoesNotThrow()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned: true);

        var context = BuildRedirectBindingContext(testData, LogoutPath(organizationId),
            "SAMLRequest", BuildSignedLogoutRequest(idpCertificate));

        Assert.True(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    // Posts a logout message to the single logout endpoint instead of a <Response> to the
    // assertion consumer service. Both endpoints sit under the same module path, so both reach
    // CouldHandleAsync.
    private static async Task<Saml2CouldHandleArrangement> ArrangeLogoutAsync(string messageXml,
        X509Certificate2 idpCertificate, X509Certificate2 spCertificate, bool wantAssertionsSigned,
        string formField = "SAMLRequest", bool featureFlagEnabled = true)
    {
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned, featureFlagEnabled);

        var context = BuildPostContext(testData, LogoutPath(organizationId), formField, messageXml);

        return new Saml2CouldHandleArrangement(samlOptions, organizationId, context);
    }

    private static string LogoutPath(string organizationId) =>
        SsoConfigurationData.BuildSaml2ModulePath(null, organizationId) + "/Logout";

    // The HTTP-Redirect binding deflates the message before base64 encoding it, so this
    // exercises the GET branch of CouldHandleAsync rather than the form branch.
    private static HttpContext BuildRedirectBindingContext(SsoTestData testData, string path,
        string queryField, string messageXml)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(messageXml);
            deflate.Write(bytes, 0, bytes.Length);
        }

        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("sso.example.com");
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = QueryString.Create(
            queryField, Convert.ToBase64String(compressed.ToArray()));
        return context;
    }

    private static string BuildSignedLogoutRequest(X509Certificate2 signingCertificate)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            "<samlp:LogoutRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
            "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
            "ID=\"_logoutrequest\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "<saml:NameID>user@test.com</saml:NameID>" +
            "</samlp:LogoutRequest>");
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!.OuterXml;
    }

    private static string BuildSignedLogoutResponse(X509Certificate2 signingCertificate)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            "<samlp:LogoutResponse xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
            "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
            "ID=\"_logoutresponse\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\" " +
            "InResponseTo=\"_logoutrequest\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "<samlp:Status><samlp:StatusCode Value=\"urn:oasis:names:tc:SAML:2.0:status:Success\" />" +
            "</samlp:Status>" +
            "</samlp:LogoutResponse>");
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!.OuterXml;
    }
}
