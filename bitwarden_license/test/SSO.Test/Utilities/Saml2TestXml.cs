using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Sustainsys.Saml2;
using CipherData = System.Security.Cryptography.Xml.CipherData;
using EncryptedData = System.Security.Cryptography.Xml.EncryptedData;
using EncryptedKey = System.Security.Cryptography.Xml.EncryptedKey;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;
using EncryptionMethod = System.Security.Cryptography.Xml.EncryptionMethod;
using KeyInfo = System.Security.Cryptography.Xml.KeyInfo;
using KeyInfoEncryptedKey = System.Security.Cryptography.Xml.KeyInfoEncryptedKey;

namespace Bit.SSO.Test.Utilities;

// Shared SAML crypto test scaffolding for Saml2AssertionSignatureVerifierTests and
// Saml2OptionsExtensionsTests. Sso.IntegrationTest keeps its own copy: that assembly is separate,
// so sharing it here would need a more central, decoupled location, which is out of scope.
internal static class Saml2TestXml
{
    private const string IdpEntityId = "https://idp.example.com/metadata";

    public static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

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

    // Encrypts an assertion element the same way a real IdP does: an AES content key wraps the
    // assertion, and the SP's certificate wraps that key. This exercises the same decrypt path
    // Sustainsys.Saml2 uses in production.
    public static string EncryptAssertion(XmlElement assertion, X509Certificate2 encryptionCertificate)
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
}
