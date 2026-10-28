using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Bit.Sso.IntegrationTest.Utilities;
using Sustainsys.Saml2;
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
/// Posts an unsigned SAML response to the assertion consumer service (ACS) URL through the whole
/// request pipeline. The organization does not require signed assertions, so
/// <c>CouldHandleAsync</c> returns true. The Sustainsys.Saml2 handler must then reject the response
/// because the response has no signature. The identity provider allows unsolicited responses, so the
/// handler reaches the signature validation. The test host returns an unhandled exception as a
/// problem details body, and the tests read the exception type, message, and stack from that body.
/// </summary>
public class Saml2AcsUnsignedAssertionTests
{
    private const string UnsignedMessage =
        "The SAML Response is not signed and contains unsigned Assertions. Response cannot be trusted.";

    private const string ValidationExceptionType = "Sustainsys.Saml2.Exceptions.Saml2ResponseFailedValidationException";

    /// <summary>
    /// A plaintext assertion without a signature fails in the Sustainsys.Saml2 signature validation.
    /// </summary>
    [Fact]
    public async Task AcsPost_UnsignedPlaintextAssertionAndWantAssertionsSignedFalse_FailsSignatureValidation()
    {
        // Arrange
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            BuildAssertion(), rsa15DeprecationFlag: true, allowUnsolicitedResponse: true);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(BuildAssertion(), includeSuccessStatus: true);

        // Act
        using var response = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, responseXml, includeSchemeQuery: true);

        // Assert
        await AssertSignatureValidationFailureAsync(response);
    }

    /// <summary>
    /// An encrypted assertion with rsa-oaep-mgf1p key transport and without a signature fails in the
    /// Sustainsys.Saml2 signature validation.
    /// </summary>
    [Fact]
    public async Task AcsPost_UnsignedOaepEncryptedAssertionAndWantAssertionsSignedFalse_FailsSignatureValidation()
    {
        // Arrange
        var spCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            BuildAssertion(), rsa15DeprecationFlag: true, spCertificate: spCertificate,
            allowUnsolicitedResponse: true);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            EncryptAssertion(BuildAssertion(), spCertificate), includeSuccessStatus: true);

        // Act
        using var response = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, responseXml, includeSchemeQuery: true);

        // Assert
        await AssertSignatureValidationFailureAsync(response);
    }

    /// <summary>
    /// Negative control. A signed assertion in the same arrangement does not fail in the
    /// Sustainsys.Saml2 signature validation. The request reaches the token validation in
    /// <c>Saml2Response.CreateClaims</c>. The request can fail there for another reason.
    /// </summary>
    [Fact]
    public async Task AcsPost_SignedPlaintextAssertionAndWantAssertionsSignedFalse_PassesSignatureValidation()
    {
        // Arrange
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            BuildAssertion(), rsa15DeprecationFlag: true, idpCertificate: idpCertificate,
            allowUnsolicitedResponse: true);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            SignAssertion(BuildAssertion(), idpCertificate), includeSuccessStatus: true);

        // Act
        using var response = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, responseXml, includeSchemeQuery: true);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(UnsignedMessage, body);
        Assert.DoesNotContain("ValidateSignature", body);

        using var problem = JsonDocument.Parse(body);
        var details = problem.RootElement.GetProperty("exception").GetProperty("details").GetString();
        Assert.Contains("Saml2Response.CreateClaims", details);
        Assert.Contains("ValidateToken", details);
    }

    private static async Task AssertSignatureValidationFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        Assert.Equal(ValidationExceptionType, root.GetProperty("title").GetString());
        Assert.Equal(UnsignedMessage, root.GetProperty("detail").GetString());
        Assert.Contains("Saml2Response.ValidateSignature",
            root.GetProperty("exception").GetProperty("details").GetString());
    }

    private static string BuildAssertion() =>
        "<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_assertion\" " +
        "Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{Saml2AcsPostHarness.IdpEntityId}</saml:Issuer>" +
        "<saml:Subject>" +
        "<saml:NameID Format=\"urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress\">" +
        "user@test.com</saml:NameID>" +
        "</saml:Subject>" +
        "</saml:Assertion>";

    private static string SignAssertion(string assertionXml, X509Certificate2 signingCertificate)
    {
        var document = XmlHelpers.XmlDocumentFromString(assertionXml);
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!.OuterXml;
    }

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
}
