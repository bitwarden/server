using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

    /// <summary>
    /// Encrypts XML into an <c>&lt;EncryptedAssertion&gt;</c> with the same method as a real
    /// identity provider. An AES content key encrypts the payload, and the service provider
    /// certificate encrypts that key. This is real XML encryption. It tests the decryption path
    /// that Sustainsys.Saml2 uses in production.
    /// </summary>
    /// <param name="payloadXml">
    /// The plaintext. It does not have to be one assertion. Decryption moves every top-level node
    /// of the plaintext into the <c>&lt;EncryptedAssertion&gt;</c>. A payload with more than one
    /// node, or with a nested assertion, tests those shapes.
    /// </param>
    public static string EncryptAssertion(string payloadXml, X509Certificate2 encryptionCertificate)
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
