using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Bit.Sso.Utilities;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Exceptions;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;
using CipherData = System.Security.Cryptography.Xml.CipherData;
using EncryptedData = System.Security.Cryptography.Xml.EncryptedData;
using EncryptedKey = System.Security.Cryptography.Xml.EncryptedKey;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;
using EncryptionMethod = System.Security.Cryptography.Xml.EncryptionMethod;
using KeyInfo = System.Security.Cryptography.Xml.KeyInfo;
using KeyInfoEncryptedKey = System.Security.Cryptography.Xml.KeyInfoEncryptedKey;

namespace Bit.SSO.Test.Utilities;

public class Saml2AssertionSignatureVerifierTests
{
    private const string ModulePath = "/saml2/test-scheme";
    private const string IdpEntityId = "https://idp.example.com/metadata";
    private const string SignatureFailureMessage = "Cannot verify SAML assertion signature.";

    [Fact]
    public void IsAuthnResponse_ResponseRoot_ReturnsTrue()
    {
        var envelope = BuildEnvelope("Response", string.Empty);

        Assert.True(Saml2AssertionSignatureVerifier.IsAuthnResponse(envelope));
    }

    [Fact]
    public void IsAuthnResponse_LogoutRequestRoot_ReturnsFalse()
    {
        var envelope = BuildEnvelope("LogoutRequest", string.Empty);

        Assert.False(Saml2AssertionSignatureVerifier.IsAuthnResponse(envelope));
    }

    [Fact]
    public void IsAuthnResponse_LogoutResponseRoot_ReturnsFalse()
    {
        var envelope = BuildEnvelope("LogoutResponse", string.Empty);

        Assert.False(Saml2AssertionSignatureVerifier.IsAuthnResponse(envelope));
    }

    [Fact]
    public void GetAssertionElements_NoAssertions_ReturnsEmpty()
    {
        var envelope = BuildEnvelope("Response", BuildStatusXml(Saml2ResponseTypes.SuccessStatus));

        Assert.Empty(Saml2AssertionSignatureVerifier.GetAssertionElements(envelope));
    }

    [Fact]
    public void GetAssertionElements_SinglePlaintextAssertion_ReturnsOneElement()
    {
        var envelope = BuildEnvelope("Response", BuildAssertionDocument().DocumentElement!.OuterXml);

        var element = Assert.Single(Saml2AssertionSignatureVerifier.GetAssertionElements(envelope));
        Assert.Equal("Assertion", element.LocalName);
    }

    [Fact]
    public void GetAssertionElements_SingleEncryptedAssertion_ReturnsOneElement()
    {
        var envelope = BuildEnvelope("Response", BuildOpaqueEncryptedAssertion());

        var element = Assert.Single(Saml2AssertionSignatureVerifier.GetAssertionElements(envelope));
        Assert.Equal("EncryptedAssertion", element.LocalName);
    }

    [Fact]
    public void GetAssertionElements_PlaintextAndEncryptedAssertions_ReturnsBothElements()
    {
        var envelope = BuildEnvelope("Response",
            BuildAssertionDocument().DocumentElement!.OuterXml + BuildOpaqueEncryptedAssertion());

        var elements = Saml2AssertionSignatureVerifier.GetAssertionElements(envelope);

        Assert.Equal(2, elements.Length);
        Assert.Equal("Assertion", elements[0].LocalName);
        Assert.Equal("EncryptedAssertion", elements[1].LocalName);
    }

    [Fact]
    public void GetAssertionElements_MatchingLocalNameInOtherNamespace_IgnoresElement()
    {
        // Only the SAML assertion namespace identifies an assertion. A same-named element in the
        // protocol namespace or a foreign namespace is not an assertion.
        var envelope = BuildEnvelope("Response",
            "<samlp:Assertion ID=\"_protocol\" />" +
            "<x:Assertion xmlns:x=\"urn:example:other\" ID=\"_foreign\" />" +
            "<x:EncryptedAssertion xmlns:x=\"urn:example:other\" />");

        Assert.Empty(Saml2AssertionSignatureVerifier.GetAssertionElements(envelope));
    }

    [Fact]
    public void HasErrorStatus_NoStatusElement_ReturnsFalse()
    {
        var envelope = BuildEnvelope("Response", string.Empty);

        Assert.False(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Fact]
    public void HasErrorStatus_StatusWithoutStatusCode_ReturnsFalse()
    {
        var envelope = BuildEnvelope("Response", "<samlp:Status />");

        Assert.False(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Fact]
    public void HasErrorStatus_SuccessStatusCode_ReturnsFalse()
    {
        var envelope = BuildEnvelope("Response", BuildStatusXml(Saml2ResponseTypes.SuccessStatus));

        Assert.False(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Fact]
    public void HasErrorStatus_ErrorStatusCode_ReturnsTrue()
    {
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus));

        Assert.True(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("&#9;&#10;")]
    public void HasErrorStatus_BlankStatusCode_ReturnsFalse(string value)
    {
        var envelope = BuildEnvelope("Response", BuildStatusXml(value));

        Assert.False(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Theory]
    [InlineData(Saml2ResponseTypes.SuccessStatus, false)]
    [InlineData(Saml2ResponseTypes.ResponderStatus, true)]
    public void HasErrorStatus_WhitespacePaddedStatusCode_TrimsBeforeComparing(string statusCode, bool expected)
    {
        var envelope = BuildEnvelope("Response", BuildStatusXml($"  {statusCode}  "));

        Assert.Equal(expected, Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
    }

    [Fact]
    public void IsSignedByIdentityProvider_PlaintextAssertionSignedByTrustedCertificate_ReturnsTrue()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var (options, idp) = BuildOptions(signingCertificate: signingCertificate);
        var element = GetSingleAssertion(BuildSignedAssertion(signingCertificate).OuterXml);

        Assert.True(Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, idp));
    }

    [Fact]
    public void IsSignedByIdentityProvider_UnsignedPlaintextAssertion_ReturnsFalse()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var (options, idp) = BuildOptions(signingCertificate: signingCertificate);
        var element = GetSingleAssertion(BuildAssertionDocument().DocumentElement!.OuterXml);

        Assert.False(Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, idp));
    }

    [Fact]
    public void IsSignedByIdentityProvider_PlaintextAssertionSignedByUntrustedCertificate_ThrowsInvalidSignature()
    {
        // XmlHelpers.IsSignedByAny returns false only for a missing <Signature>. A signature that
        // no trusted key verifies raises InvalidSignatureException, and this method does not catch it.
        var trustedCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var untrustedCertificate = CreateSelfSignedCertificate("CN=Untrusted IdP");
        var (options, idp) = BuildOptions(signingCertificate: trustedCertificate);
        var element = GetSingleAssertion(BuildSignedAssertion(untrustedCertificate).OuterXml);

        Assert.Throws<InvalidSignatureException>(
            () => Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, idp));
    }

    [Fact]
    public void IsSignedByIdentityProvider_EncryptedAssertionWithValidSignature_ReturnsTrue()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, idp) = BuildOptions(decryptionCertificate, signingCertificate);
        var element = GetSingleAssertion(
            EncryptAssertion(BuildSignedAssertion(signingCertificate), decryptionCertificate));

        Assert.True(Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, idp));
    }

    [Fact]
    public void IsSignedByIdentityProvider_EncryptedAssertionWithoutSignature_ReturnsFalse()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, idp) = BuildOptions(decryptionCertificate, signingCertificate);
        var element = GetSingleAssertion(
            EncryptAssertion(BuildAssertionDocument().DocumentElement!, decryptionCertificate));

        Assert.False(Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, idp));
    }

    [Fact]
    public void IsSignedByIdentityProvider_UndecryptableAssertion_ReturnsFalseWithoutCheckingSignature()
    {
        // The IdP encrypts for a certificate the SP does not hold, so the decryptor returns null.
        // The identity provider is null: any signature check dereferences it and throws, so a
        // false result proves the signature check never runs.
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var otherCertificate = CreateSelfSignedCertificate("CN=Other SP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, _) = BuildOptions(decryptionCertificate);
        var element = GetSingleAssertion(
            EncryptAssertion(BuildSignedAssertion(signingCertificate), otherCertificate));

        Assert.Null(Saml2EncryptedAssertionDecryptor.TryDecryptAssertion(
            element, options.SPOptions.DecryptionServiceCertificates));
        Assert.False(Saml2AssertionSignatureVerifier.IsSignedByIdentityProvider(element, options, null!));
    }

    [Theory]
    [InlineData("LogoutRequest", false)]
    [InlineData("LogoutRequest", true)]
    [InlineData("LogoutResponse", false)]
    [InlineData("LogoutResponse", true)]
    public void EnsureAssertionsSigned_LogoutEnvelope_DoesNotThrow(string rootName, bool includeUnsignedAssertion)
    {
        var (options, idp) = BuildOptions();
        var envelope = BuildEnvelope(rootName,
            includeUnsignedAssertion ? BuildAssertionDocument().DocumentElement!.OuterXml : string.Empty);

        Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp);
    }

    [Fact]
    public void EnsureAssertionsSigned_ErrorStatusWithNoAssertions_DoesNotThrow()
    {
        var (options, idp) = BuildOptions();
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus));

        Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp);
    }

    [Fact]
    public void EnsureAssertionsSigned_SuccessStatusWithNoAssertions_Throws()
    {
        var (options, idp) = BuildOptions();
        var envelope = BuildEnvelope("Response", BuildStatusXml(Saml2ResponseTypes.SuccessStatus));

        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    [Fact]
    public void EnsureAssertionsSigned_NoStatusWithNoAssertions_Throws()
    {
        var (options, idp) = BuildOptions();
        var envelope = BuildEnvelope("Response", string.Empty);

        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    [Fact]
    public void EnsureAssertionsSigned_ZeroAssertionsWithoutErrorStatus_ThrowsFailClosed()
    {
        // With zero assertions, Any() over the assertions returns false, so only the explicit
        // Length == 0 guard rejects the envelope. An unreadable status code keeps HasErrorStatus false.
        var (options, idp) = BuildOptions();
        var envelope = BuildEnvelope("Response", "<samlp:Status><samlp:StatusCode Value=\"\" /></samlp:Status>");

        Assert.Empty(Saml2AssertionSignatureVerifier.GetAssertionElements(envelope));
        Assert.False(Saml2AssertionSignatureVerifier.HasErrorStatus(envelope));
        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    [Fact]
    public void EnsureAssertionsSigned_AllAssertionsSigned_DoesNotThrow()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, idp) = BuildOptions(decryptionCertificate, signingCertificate);
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) +
            BuildSignedAssertion(signingCertificate, "_plaintext").OuterXml +
            EncryptAssertion(BuildSignedAssertion(signingCertificate, "_encrypted"), decryptionCertificate));

        Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureAssertionsSigned_OneUnsignedAssertion_ThrowsRegardlessOfOrder(bool unsignedFirst)
    {
        // Distinct IDs keep each signature reference bound to its own assertion.
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var (options, idp) = BuildOptions(signingCertificate: signingCertificate);
        var signed = BuildSignedAssertion(signingCertificate, "_signed").OuterXml;
        var unsigned = BuildAssertionDocument("_unsigned").DocumentElement!.OuterXml;
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) +
            (unsignedFirst ? unsigned + signed : signed + unsigned));

        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    [Fact]
    public void EnsureAssertionsSigned_SignedPlaintextWithUnsignedEncryptedSibling_Throws()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, idp) = BuildOptions(decryptionCertificate, signingCertificate);
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) +
            BuildSignedAssertion(signingCertificate, "_plaintext").OuterXml +
            EncryptAssertion(BuildAssertionDocument("_encrypted").DocumentElement!, decryptionCertificate));

        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    [Fact]
    public void EnsureAssertionsSigned_SignedEncryptedWithUnsignedPlaintextSibling_Throws()
    {
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var (options, idp) = BuildOptions(decryptionCertificate, signingCertificate);
        var envelope = BuildEnvelope("Response",
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) +
            EncryptAssertion(BuildSignedAssertion(signingCertificate, "_encrypted"), decryptionCertificate) +
            BuildAssertionDocument("_plaintext").DocumentElement!.OuterXml);

        var exception = Assert.Throws<Exception>(
            () => Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp));
        Assert.Equal(SignatureFailureMessage, exception.Message);
    }

    private static (Saml2Options Options, IdentityProvider Idp) BuildOptions(
        X509Certificate2? decryptionCertificate = null, X509Certificate2? signingCertificate = null)
    {
        var spOptions = new SPOptions
        {
            EntityId = new EntityId("https://sso.bitwarden.com" + ModulePath),
            ModulePath = ModulePath,
            WantAssertionsSigned = true,
        };
        if (decryptionCertificate != null)
        {
            spOptions.ServiceCertificates.Add(decryptionCertificate);
        }

        var idp = new IdentityProvider(new EntityId(IdpEntityId), spOptions)
        {
            Binding = Saml2BindingType.HttpPost,
            SingleSignOnServiceUrl = new Uri("https://idp.example.com/sso"),
        };
        if (signingCertificate != null)
        {
            idp.SigningKeys.AddConfiguredKey(signingCertificate);
        }

        var options = new Saml2Options { SPOptions = spOptions };
        options.IdentityProviders.Add(idp);
        return (options, idp);
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

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

    // Encrypts an assertion element the same way a real IdP does: an AES content key wraps the
    // assertion, and the SP's certificate wraps that key. This exercises the same decrypt path
    // Sustainsys.Saml2 uses in production.
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

    // An encrypted assertion shape for tests that only select elements and never decrypt.
    private static string BuildOpaqueEncryptedAssertion() =>
        "<saml:EncryptedAssertion>" +
        "<xenc:EncryptedData xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\">" +
        "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedData>" +
        "</saml:EncryptedAssertion>";

    // SAML Core 3.2.2.2 nests the second-level status code inside the top-level one.
    private static string BuildStatusXml(string topLevelCode, string? secondLevelCode = null) =>
        $"<samlp:Status><samlp:StatusCode Value=\"{topLevelCode}\">" +
        (secondLevelCode == null ? string.Empty : $"<samlp:StatusCode Value=\"{secondLevelCode}\" />") +
        "</samlp:StatusCode></samlp:Status>";

    private static XmlElement BuildEnvelope(string rootName, string content)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            $"<samlp:{rootName} xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
            "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
            "ID=\"_envelope\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            content +
            $"</samlp:{rootName}>");

        return document.DocumentElement!;
    }

    // Places the assertion inside a Response envelope, so signature references resolve the same
    // way they do in production.
    private static XmlElement GetSingleAssertion(string assertionXml)
    {
        var envelope = BuildEnvelope("Response", assertionXml);
        return envelope.ChildNodes
            .OfType<XmlElement>()
            .Single(e => e.NamespaceURI == Saml2Namespaces.Saml2Name && e.LocalName != "Issuer");
    }
}
