using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities.Saml2;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Exceptions;
using Xunit;
using static Bit.Sso.IntegrationTest.Utilities.Saml2CouldHandleTestHelpers;
using static Bit.Sso.IntegrationTest.Utilities.Saml2MessageBuilder;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;

namespace Bit.Sso.IntegrationTest.Saml2.RequestMatching;

/// <summary>
/// Exercises the assertion signature check in <c>CouldHandleAsync</c> through the real, database-backed
/// <c>Saml2Options</c> object that <c>DynamicAuthenticationScheme</c> holds. Covers plaintext, encrypted,
/// mixed, and nested assertions, untrusted and undecryptable assertions, messages that the scheme declines,
/// and how the check orders with the encrypted-assertion key-transport algorithm check.
/// </summary>
public class Saml2CouldHandleAssertionSignatureTests
{
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
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(signedAssertion.OuterXml, spCertificate);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_SignedOaepEncryptedAssertionWithPeerKeyAndRetrievalMethodAndRsa15DeprecationFlagOn_ReturnsTrue()
    {
        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData and link the two
        // with a ds:RetrievalMethod. Sustainsys.Saml2 must decrypt this layout.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var signedAssertion = BuildSignedAssertion(idpCertificate);
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(
            signedAssertion.OuterXml, spCertificate, keyBesideData: true);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true,
            rsa15DeprecationFlag: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedEncryptedAssertionAndWantAssertionsSigned_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var unsignedAssertion = BuildAssertionDocument().DocumentElement!.OuterXml;
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(unsignedAssertion, spCertificate);

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
            Saml2AcsPostHarness.EncryptAssertion(BuildAssertionDocument().DocumentElement!.OuterXml, spCertificate);

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
            Saml2AcsPostHarness.EncryptAssertion(payload, spCertificate), idpCertificate, spCertificate,
            wantAssertionsSigned: true);

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
            Saml2AcsPostHarness.EncryptAssertion(BuildSignedAssertion(idpCertificate).OuterXml, foreignCertificate),
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_SignedEncryptedAssertionWithRsa15KeyTransportAndWantAssertionsSigned_ReturnsTrueWithFlagOffAndThrowsWithFlagOn(
        bool rsa15DeprecationFlag)
    {
        // The signature sits inside the encrypted assertion. With the flag off, a true result proves that
        // CouldHandleAsync decrypted the rsa-1_5 key transport. With the flag on, the gate throws before
        // the signature check decrypts anything.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(
            BuildSignedAssertion(idpCertificate).OuterXml, spCertificate, EncryptedXml.XmlEncRSA15Url);

        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned: true, rsa15EmailCooldownInDays: 0,
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId),
            "SAMLResponse", BuildResponseXml(encryptedAssertionXml));

        await AssertReturnsTrueOrThrowsGateAsync(samlOptions, organizationId, context, rsa15DeprecationFlag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_UnsignedEncryptedAssertionWithRsa15KeyTransportAndWantAssertionsSignedFalse_ReturnsTrueWithFlagOffAndThrowsWithFlagOn(
        bool rsa15DeprecationFlag)
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(
            BuildAssertionDocument().DocumentElement!.OuterXml, spCertificate, EncryptedXml.XmlEncRSA15Url);

        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned: false, rsa15EmailCooldownInDays: 0,
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId),
            "SAMLResponse", BuildResponseXml(encryptedAssertionXml));

        await AssertReturnsTrueOrThrowsGateAsync(samlOptions, organizationId, context, rsa15DeprecationFlag);
    }

    [Fact]
    public async Task CouldHandleAsync_SignedEncryptedAssertionWithOaepKeyTransportAndWantAssertionsSignedAndRsa15DeprecationFlagOn_ReturnsTrue()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(
            BuildSignedAssertion(idpCertificate).OuterXml, spCertificate, EncryptedXml.XmlEncRSAOAEPUrl);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true,
            rsa15DeprecationFlag: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnsignedEncryptedAssertionWithRsa15KeyTransportAndWantAssertionsSignedAndRsa15DeprecationFlagOn_ThrowsTheGateMessage()
    {
        // The gate must precede the signature check. A check that decrypted first would throw
        // "Cannot verify SAML assertion signature." for this unsigned assertion.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var encryptedAssertionXml = Saml2AcsPostHarness.EncryptAssertion(
            BuildAssertionDocument().DocumentElement!.OuterXml, spCertificate, EncryptedXml.XmlEncRSA15Url);

        var arrangement = await ArrangeAsync(
            encryptedAssertionXml, idpCertificate, spCertificate, wantAssertionsSigned: true,
            rsa15DeprecationFlag: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    private static async Task AssertReturnsTrueOrThrowsGateAsync(
        Saml2Options samlOptions, string organizationId, HttpContext context, bool rsa15DeprecationFlag)
    {
        if (rsa15DeprecationFlag)
        {
            var exception = await Assert.ThrowsAsync<Exception>(
                () => samlOptions.CouldHandleAsync(organizationId, context));
            Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
        }
        else
        {
            Assert.True(await samlOptions.CouldHandleAsync(organizationId, context));
        }
    }
}
