using System.Xml;
using Bit.Sso.Utilities.Saml2;
using Sustainsys.Saml2;

namespace Bit.SSO.Test.Utilities;

public class Saml2EncryptedAssertionInspectorTests
{
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaepMgf1P = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    [Fact]
    public void UsesRsa15KeyTransport_Rsa15Key_ReturnsTrue()
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(RsaPkcs1));

        Assert.True(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Theory]
    [InlineData(RsaOaepMgf1P)]
    [InlineData(RsaOaep)]
    public void UsesRsa15KeyTransport_AcceptedAlgorithm_ReturnsFalse(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));

        Assert.False(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_PlaintextAssertion_ReturnsFalse()
    {
        var envelope = BuildEnvelope("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");

        Assert.False(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_NoEncryptedKey_ReturnsFalse()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Assert.False(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#kw-aes256")]
    [InlineData("urn:example:unknown-algorithm")]
    [InlineData("rsa-1_5\nlevel=something-else")]
    public void UsesRsa15KeyTransport_UnrecognizedAlgorithm_ReturnsFalse(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));

        Assert.False(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_Rsa15BesideAcceptedKeyInOneAssertion_ReturnsTrue()
    {
        // One assertion can hold more than one key. An accepted key must not hide rsa-1_5.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Assert.True(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_Rsa15InSecondAssertion_ReturnsTrue()
    {
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaOaep) +
            BuildNestedEncryptedAssertion(RsaPkcs1));

        Assert.True(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_KeyBesideEncryptedData_ReturnsTrue()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey Id=\"_key\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "<xenc:ReferenceList><xenc:DataReference URI=\"#_data\" /></xenc:ReferenceList>" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedData Id=\"_data\">" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Assert.True(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope));
    }

    [Fact]
    public void UsesRsa15KeyTransport_NullEnvelope_ReturnsFalse()
    {
        Assert.False(Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(null!));
    }

    private static string BuildNestedEncryptedAssertion(string algorithm) =>
        "<saml:EncryptedAssertion>" +
        "<xenc:EncryptedData>" +
        "<ds:KeyInfo>" +
        "<xenc:EncryptedKey>" +
        $"<xenc:EncryptionMethod Algorithm=\"{EscapeAttributeValue(algorithm)}\" />" +
        "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedKey>" +
        "</ds:KeyInfo>" +
        "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedData>" +
        "</saml:EncryptedAssertion>";

    private static XmlElement BuildEnvelope(string assertionElement)
    {
        var document = XmlHelpers.XmlDocumentFromString(
            "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
            "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
            "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
            "xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\" " +
            "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
            "<saml:Issuer>https://idp.example.com/metadata</saml:Issuer>" +
            assertionElement +
            "</samlp:Response>");

        return document.DocumentElement!;
    }

    private static string EscapeAttributeValue(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;").Replace("\n", "&#10;");
}
