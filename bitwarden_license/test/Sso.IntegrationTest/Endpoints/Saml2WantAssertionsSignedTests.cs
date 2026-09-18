using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
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
        // Before PM-42982, this always threw. The pre-flight check only ever looked
        // for a plaintext <Assertion> node, which never exists in this form.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedAssertion = BuildSignedAssertion(idpCertificate);
        var encryptedAssertionXml = EncryptAssertion(signedAssertion, spCertificate);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedEncryptedAssertionAndWantAssertionsSigned_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var unsignedAssertion = BuildAssertionDocument().DocumentElement!;
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
        // Before PM-42982, the pre-flight check found only the first <Assertion> node and
        // stopped, so a signed plaintext assertion let an unsigned encrypted sibling
        // through. This branch checks every assertion element in the envelope, so the
        // unsigned sibling now fails the whole response.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedPlaintextAssertion = BuildSignedAssertion(idpCertificate);
        var unsignedEncryptedAssertionXml =
            EncryptAssertion(BuildAssertionDocument().DocumentElement!, spCertificate);

        var arrangement = await ArrangeAsync(
            signedPlaintextAssertion.OuterXml + unsignedEncryptedAssertionXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    private static (X509Certificate2 IdpCertificate, X509Certificate2 SpCertificate) BuildCertificates() =>
        (CreateSelfSignedCertificate("CN=Test IdP"), CreateSelfSignedCertificate("CN=Test SP"));

    private static async Task<Arrangement> ArrangeAsync(string assertionElement,
        X509Certificate2 idpCertificate, X509Certificate2 spCertificate, bool wantAssertionsSigned)
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = IdpEntityId,
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(idpCertificate.RawData),
                SpWantAssertionsSigned = wantAssertionsSigned,
            }))
            .WithSamlSigningCertificate(spCertificate)
            .BuildAsync();

        var organizationId = testData.Organization!.Id;
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId.ToString());
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        var responseXml = BuildResponseXml(assertionElement);
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

        return new Arrangement(samlOptions, organizationId.ToString(), context);
    }

    private sealed record Arrangement(Saml2Options SamlOptions, string Scheme, HttpContext Context);

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    private static XmlDocument BuildAssertionDocument() =>
        XmlHelpers.XmlDocumentFromString(
            "<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_assertion\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "</saml:Assertion>");

    private static XmlElement BuildSignedAssertion(X509Certificate2 signingCertificate)
    {
        var document = BuildAssertionDocument();
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!;
    }

    // Encrypts an assertion element the same way a real IdP does: an AES content key wraps the
    // assertion, and the SP's certificate wraps that key. This is real XML encryption, not a fixed
    // fixture, so it exercises the same decrypt path Sustainsys.Saml2 uses in production.
    private static string EncryptAssertion(XmlElement assertion, X509Certificate2 encryptionCertificate)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            "<saml:EncryptedAssertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" />");
        var importedAssertion = (XmlElement)document.ImportNode(assertion, deep: true);
        document.DocumentElement!.AppendChild(importedAssertion);

        using var contentKey = Aes.Create();
        contentKey.KeySize = 256;
        var cipherValue = new EncryptedXml().EncryptData(importedAssertion, contentKey, content: false);

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

        EncryptedXml.ReplaceElement(importedAssertion, encryptedData, content: false);

        return document.DocumentElement!.OuterXml;
    }

    private static string BuildResponseXml(string assertionElement) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";
}
