using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities;
using Bit.Sso.Utilities.Saml2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Exceptions;
using Xunit;
using CipherData = System.Security.Cryptography.Xml.CipherData;
using EncryptedData = System.Security.Cryptography.Xml.EncryptedData;
using EncryptedKey = System.Security.Cryptography.Xml.EncryptedKey;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;
using EncryptionMethod = System.Security.Cryptography.Xml.EncryptionMethod;
using KeyInfo = System.Security.Cryptography.Xml.KeyInfo;
using KeyInfoEncryptedKey = System.Security.Cryptography.Xml.KeyInfoEncryptedKey;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Exercises CouldHandleAsync's WantAssertionsSigned check through the real, database-backed
/// Saml2Options object built by DynamicAuthenticationSchemeProvider, the same way
/// Saml2AssertionKeyTransportAlgorithmVerificationTests does for the key-transport-algorithm check.
/// </summary>
public class Saml2WantAssertionsSignedTests
{

    private const string IdpEntityId = "https://idp.example.com";

    [Fact]
    public async Task CouldHandleAsync_SignedPlaintextAssertionAndWantAssertionsSigned_DoesNotThrow()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedAssertion = BuildSignedAssertion(idpCertificate);

        var arrangement = await ArrangeAsync(
            signedAssertion.OuterXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedPlaintextAssertionAndWantAssertionsSigned_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var unsignedAssertion = BuildAssertionDocument().DocumentElement!.OuterXml;

        var arrangement = await ArrangeAsync(
            unsignedAssertion, idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedPlaintextAssertionAndWantAssertionsSignedFalse_DoesNotThrow()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var unsignedAssertion = BuildAssertionDocument().DocumentElement!.OuterXml;

        var arrangement = await ArrangeAsync(
            unsignedAssertion, idpCertificate, spCertificate, wantAssertionsSigned: false);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_SignedEncryptedAssertionAndWantAssertionsSigned_DoesNotThrow()
    {
        // Ensures that WantAssertionsSigned operates correctly on an EncryptedAssertion.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedAssertion = BuildSignedAssertion(idpCertificate);
        var encryptedAssertionXml = EncryptAssertion(signedAssertion.OuterXml, spCertificate);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedEncryptedAssertionAndWantAssertionsSigned_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var unsignedAssertion = BuildAssertionDocument().DocumentElement!.OuterXml;
        var encryptedAssertionXml = EncryptAssertion(unsignedAssertion, spCertificate);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SignedPlaintextAssertionWithUnsignedEncryptedSiblingAndWantAssertionsSigned_Throws()
    {
        // Check every assertion element in the envelope.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedPlaintextAssertion = BuildSignedAssertion(idpCertificate);
        var unsignedEncryptedAssertionXml =
            EncryptAssertion(BuildAssertionDocument().DocumentElement!.OuterXml, spCertificate);

        var arrangement = await ArrangeAsync(
            signedPlaintextAssertion.OuterXml + unsignedEncryptedAssertionXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_EncryptedAssertionNestingUnsignedAssertionAheadOfSignedOne_Throws()
    {
        // Decryption moves every top-level node of the plaintext into the <EncryptedAssertion>.
        // An unsigned assertion in an <Advice> element can come before a signed top-level assertion.
        // Sustainsys.Saml2 builds claims from the first descendant assertion in document order.
        // The check must read that same element. If the check reads the first direct child, the
        // check verifies the signed assertion and accepts the unsigned assertion.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var payload =
            "<saml:Advice xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\">" +
            BuildAssertionDocument("_nested").DocumentElement!.OuterXml +
            "</saml:Advice>" +
            BuildSignedAssertion(idpCertificate, "_signed").OuterXml;

        var arrangement = await ArrangeAsync(
            EncryptAssertion(payload, spCertificate), idpCertificate, spCertificate,
            wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

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

    [Fact]
    public async Task CouldHandleAsync_ErrorStatusResponseWithNoAssertions_DoesNotThrow()
    {
        // An identity provider that refuses to authenticate returns a <Response> with an error
        // status and no assertions, per SAML Profiles 4.1.4.1. The message is well-formed and has
        // no assertion to check, so this check must let it through and let the handler pipeline
        // report the real status.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus),
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_ErrorStatusResponseWithUnsignedAssertion_Throws()
    {
        // Guards against a too-broad status gate. An error status must not excuse an assertion
        // that is actually present. A response in this shape breaks SAML Profiles 4.1.4.1, so
        // its assertions get the same scrutiny as any other.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus) +
            BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_MissingStatusElementWithUnsignedAssertion_Throws()
    {
        // Fail-closed guard. <samlp:Status> is mandatory in a <Response>, but a caller controls
        // the payload and can omit it. An absent element must never be read as "not a success".
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SuccessStatusResponseWithUnsignedAssertion_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) + BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SuccessStatusResponseWithNoAssertions_Throws()
    {
        // A successful authentication with no assertion is unexpected. 
        // The status gate must not turn this into a pass.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus), idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_AssertionSignedByUntrustedKey_ThrowsInvalidSignature()
    {
        // XmlHelpers.IsSignedByAny returns false only when an assertion carries no <Signature> at
        // all. A signature that is present but does not verify against the configured keys raises
        // InvalidSignatureException.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var untrustedCertificate = CreateSelfSignedCertificate("CN=Untrusted IdP");

        var arrangement = await ArrangeAsync(
            BuildSignedAssertion(untrustedCertificate).OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        await Assert.ThrowsAsync<InvalidSignatureException>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UndecryptableEncryptedAssertion_Throws()
    {
        // An <EncryptedAssertion> encrypted for a certificate this service provider does not hold
        // cannot be read, so it can never be shown to be signed. The decryptor must return null and
        // treat the assertion as unsigned.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var foreignCertificate = CreateSelfSignedCertificate("CN=Other SP");

        var arrangement = await ArrangeAsync(
            EncryptAssertion(BuildSignedAssertion(idpCertificate).OuterXml, foreignCertificate),
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedAssertionBeforeSignedAssertion_Throws()
    {
        // Order must not matter. The unsigned assertion comes first here, where the sibling test
        // places it second.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildAssertionDocument("_unsigned").DocumentElement!.OuterXml +
            BuildSignedAssertion(idpCertificate, "_signed").OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Theory]
    [InlineData("<samlp:Status />")]
    [InlineData("<samlp:Status><samlp:StatusCode Value=\"\" /></samlp:Status>")]
    [InlineData("<samlp:Status><samlp:StatusCode Value=\"   \" /></samlp:Status>")]
    public async Task CouldHandleAsync_StatusWithoutReadableCodeAndNoAssertions_Throws(string statusXml)
    {
        // Fail-closed. Only a status code that reads as a real error excuses a response carrying
        // no assertions. A <Status> element with nothing usable inside it must not.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            statusXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_WhitespacePaddedErrorStatusWithNoAssertions_DoesNotThrow()
    {
        // XML keeps leading and trailing spaces in an attribute value. The status read trims
        // before comparing, so padding does not turn a refusal into a signature failure.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            $"<samlp:Status><samlp:StatusCode Value=\"  {Saml2ResponseTypes.ResponderStatus}  \" /></samlp:Status>",
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_ResponseWithNoAssertionElementsAndWantAssertionsSigned_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            string.Empty, idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_IssuerDoesNotMatchIdp_ReturnsFalseWithoutThrowing()
    {
        // Scheme-selection invariant. A message from another identity provider must decline the
        // scheme, not throw, even with an unsigned assertion and the setting on. Any change to
        // the signature check must keep this ordering.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned: true);

        var foreignResponse = BuildResponseXml(
            BuildAssertionDocument().DocumentElement!.OuterXml, "https://other-idp.example.com");
        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId), "SAMLResponse", foreignResponse);

        Assert.False(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnparseablePayload_ReturnsFalseWithoutThrowing()
    {
        // Scheme-selection invariant. Malformed input must decline the scheme quietly so the
        // middleware can try the next one.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned: true);

        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Path = SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = "not-base64-at-all!!",
        });

        Assert.False(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    private static (X509Certificate2 IdpCertificate, X509Certificate2 SpCertificate) BuildCertificates() =>
        (CreateSelfSignedCertificate("CN=Test IdP"), CreateSelfSignedCertificate("CN=Test SP"));

    // Builds the real, database-backed scheme and returns the pieces each arrangement needs.
    // Every test in this file exercises the multi-assertion signature verifier
    // (Saml2AssertionSignatureVerifier.EnsureAssertionsSigned), so PM42982_WantAssertionsSigned
    // must be enabled here. A test that wants the legacy, pre-flag branch instead must pass
    // featureFlagEnabled: false.
    private static async Task<(SsoTestData TestData, Saml2Options SamlOptions, string OrganizationId)>
        BuildSchemeAsync(X509Certificate2 idpCertificate, X509Certificate2 spCertificate,
            bool wantAssertionsSigned, bool featureFlagEnabled = true)
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = IdpEntityId,
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpSingleLogoutServiceUrl = "https://idp.example.com/slo",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(idpCertificate.RawData),
                SpWantAssertionsSigned = wantAssertionsSigned,
            }))
            .WithSamlSigningCertificate(spCertificate)
            .WithPM42982WantAssertionsSignedFlag(featureFlagEnabled)
            .BuildAsync();

        var organizationId = testData.Organization!.Id.ToString();
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId);
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        return (testData, samlOptions, organizationId);
    }

    private static async Task<Arrangement> ArrangeAsync(string assertionElement,
        X509Certificate2 idpCertificate, X509Certificate2 spCertificate, bool wantAssertionsSigned,
        bool featureFlagEnabled = true)
    {
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned, featureFlagEnabled);

        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId),
            "SAMLResponse",
            BuildResponseXml(assertionElement));

        return new Arrangement(samlOptions, organizationId, context);
    }

    // Posts a logout message to the single logout endpoint instead of a <Response> to the
    // assertion consumer service. Both endpoints sit under the same module path, so both reach
    // CouldHandleAsync.
    private static async Task<Arrangement> ArrangeLogoutAsync(string messageXml,
        X509Certificate2 idpCertificate, X509Certificate2 spCertificate, bool wantAssertionsSigned,
        string formField = "SAMLRequest", bool featureFlagEnabled = true)
    {
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned, featureFlagEnabled);

        var context = BuildPostContext(testData, LogoutPath(organizationId), formField, messageXml);

        return new Arrangement(samlOptions, organizationId, context);
    }

    private static string LogoutPath(string organizationId) =>
        SsoConfigurationData.BuildSaml2ModulePath(null, organizationId) + "/Logout";

    private static HttpContext BuildPostContext(SsoTestData testData, string path, string formField,
        string messageXml)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [formField] = Convert.ToBase64String(Encoding.UTF8.GetBytes(messageXml)),
        });
        return context;
    }

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

    private sealed record Arrangement(Saml2Options SamlOptions, string Scheme, HttpContext Context);

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    // Callers that place two plaintext assertions in one envelope must give each a distinct ID,
    // so signature references resolve to the intended element.
    private static XmlDocument BuildAssertionDocument(string assertionId = "_assertion") =>
        XmlHelpers.XmlDocumentFromString(
            $"<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"{assertionId}\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "</saml:Assertion>");

    private static XmlElement BuildSignedAssertion(X509Certificate2 signingCertificate,
        string assertionId = "_assertion")
    {
        var document = BuildAssertionDocument(assertionId);
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!;
    }

    // Encrypts XML into an <EncryptedAssertion> with the same method as a real identity provider.
    // An AES content key encrypts the payload, and the service provider certificate encrypts that key.
    // This is real XML encryption, not a fixed fixture. It tests the decryption path that
    // Sustainsys.Saml2 uses in production. The payload does not have to be one assertion.
    // Decryption moves every top-level node of the payload into the <EncryptedAssertion>.
    // A payload with more than one node, or with a nested assertion, tests those shapes.
    private static string EncryptAssertion(string payloadXml, X509Certificate2 encryptionCertificate)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            "<saml:EncryptedAssertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" />");

        using var contentKey = Aes.Create();
        contentKey.KeySize = 256;
        var cipherValue = new EncryptedXml().EncryptData(Encoding.UTF8.GetBytes(payloadXml), contentKey);

        var encryptedData = new EncryptedData
        {
            Type = EncryptedXml.XmlEncElementUrl,
            EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncAES256Url),
        };
        encryptedData.CipherData.CipherValue = cipherValue;
        encryptedData.KeyInfo = new KeyInfo();
        var encryptedKey = new EncryptedKey
        {
            EncryptionMethod = new EncryptionMethod(EncryptedXml.XmlEncRSAOAEPUrl),
            CipherData = new CipherData(
                EncryptedXml.EncryptKey(contentKey.Key, encryptionCertificate.GetRSAPublicKey()!, useOAEP: true)),
        };
        encryptedData.KeyInfo.AddClause(new KeyInfoEncryptedKey(encryptedKey));

        document.DocumentElement!.AppendChild(document.ImportNode(encryptedData.GetXml(), deep: true));

        return document.DocumentElement!.OuterXml;
    }

    // SAML Core 3.2.2.2 nests the second-level status code inside the top-level one, so this
    // also exercises reading the outer Value rather than the inner one.
    private static string BuildStatusXml(string topLevelCode, string? secondLevelCode = null) =>
        $"<samlp:Status><samlp:StatusCode Value=\"{topLevelCode}\">" +
        (secondLevelCode == null ? string.Empty : $"<samlp:StatusCode Value=\"{secondLevelCode}\" />") +
        "</samlp:StatusCode></samlp:Status>";

    private static string BuildResponseXml(string assertionElement, string? issuer = null) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{issuer ?? IdpEntityId}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

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
