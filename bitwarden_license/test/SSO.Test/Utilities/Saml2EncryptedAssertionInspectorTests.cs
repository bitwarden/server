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

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_PlaintextAssertion_DoesNotThrow()
    {
        var envelope = BuildEnvelope("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_NoAssertions_DoesNotThrow()
    {
        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(BuildEnvelope(string.Empty));
    }

    [Theory]
    [InlineData(RsaOaepMgf1P)]
    [InlineData(RsaOaep)]
    public void EnsureAcceptedKeyTransportAlgorithms_AcceptedAlgorithm_DoesNotThrow(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_TwoAcceptedKeysInOneAssertion_DoesNotThrow()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            $"<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" /></xenc:EncryptedKey>" +
            $"<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"{RsaOaep}\" /></xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_DataEncryptionMethodBesideAcceptedKey_DoesNotThrow()
    {
        // The data encryption algorithm is not a key transport algorithm.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:EncryptionMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#aes256-cbc\" />" +
            "<ds:KeyInfo>" +
            $"<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"{RsaOaep}\" /></xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_NestedRsa15Key_Throws()
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(RsaPkcs1));

        var exception = Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));

        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_Rsa15KeyBesideEncryptedData_Throws()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey Id=\"_key\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "<xenc:ReferenceList><xenc:DataReference URI=\"#_data\" /></xenc:ReferenceList>" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedData Id=\"_data\">" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_AcceptedKeyBeforeRsa15KeyInOneAssertion_Throws()
    {
        // Every key in an assertion must use an accepted algorithm.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            $"<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" /></xenc:EncryptedKey>" +
            $"<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" /></xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_OaepAssertionAndRsa15Assertion_Throws()
    {
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaOaep) + BuildNestedEncryptedAssertion(RsaPkcs1));

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#kw-aes256")]
    [InlineData("urn:example:unknown-algorithm")]
    [InlineData(" http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p ")]
    [InlineData("HTTP://WWW.W3.ORG/2001/04/XMLENC#RSA-OAEP-MGF1P")]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureAcceptedKeyTransportAlgorithms_UnacceptedAlgorithm_Throws(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));

        var exception = Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));

        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_LongAlgorithmWithNewline_ThrowsConstantMessage()
    {
        var algorithm = "rsa-1_5\nlevel=something-else" + new string('a', 10 * 1024);
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));

        var exception = Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));

        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    [Theory]
    [InlineData("<xenc:EncryptedData><xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData></xenc:EncryptedData>")]
    [InlineData("<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>")]
    [InlineData("<xenc:EncryptedKey />")]
    public void EnsureAcceptedKeyTransportAlgorithms_MissingKeyOrAlgorithm_Throws(string content)
    {
        var envelope = BuildEnvelope($"<saml:EncryptedAssertion>{content}</saml:EncryptedAssertion>");

        var exception = Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));

        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_NamespacedAlgorithmAttributeOnly_Throws()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod xenc:Algorithm=\"{RsaOaepMgf1P}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_Rsa15KeyInsideEncryptedId_Throws()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<saml:EncryptedID>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedID>" +
            "</saml:EncryptedAssertion>");

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_EncryptedAssertionBelowTopLevel_IsNotInspected()
    {
        var envelope = BuildEnvelope(
            $"<saml:Advice>{BuildNestedEncryptedAssertion(RsaPkcs1)}</saml:Advice>");

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_Rsa15KeyInEncryptedAssertionWithAlternatePrefixOrDefaultNamespace_Throws()
    {
        const string encryptedKey =
            "<xenc:EncryptedKey xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" /></xenc:EncryptedKey>";
        var prefixed = BuildEnvelope(
            "<s2:EncryptedAssertion xmlns:s2=\"urn:oasis:names:tc:SAML:2.0:assertion\">" +
            encryptedKey + "</s2:EncryptedAssertion>");
        var defaultNamespace = BuildEnvelope(
            "<EncryptedAssertion xmlns=\"urn:oasis:names:tc:SAML:2.0:assertion\">" +
            encryptedKey + "</EncryptedAssertion>");

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(prefixed));
        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(defaultNamespace));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_SecondEncryptionMethodInOneKey_IsNotRead()
    {
        // EncryptedKey.LoadXml reads the first EncryptionMethod, so decryption never uses the second one.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_PeerKeyReferencedByRetrievalMethod_DoesNotThrow()
    {
        var envelope = BuildEnvelope(BuildRetrievalMethodAssertion("#_key", "<xenc:EncryptedData Id=\"_data\" />"));

        Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_RetrievalMethodNamesNonKeyElement_Throws()
    {
        var envelope = BuildEnvelope(BuildRetrievalMethodAssertion("#_other", "<xenc:EncryptedData Id=\"_other\" />"));

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Fact]
    public void EnsureAcceptedKeyTransportAlgorithms_RetrievalMethodNamesKeyAndNonKeyElement_Throws()
    {
        var envelope = BuildEnvelope(BuildRetrievalMethodAssertion("#_key", "<xenc:EncryptedData Id=\"_key\" />"));

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("ID")]
    public void EnsureAcceptedKeyTransportAlgorithms_RetrievalMethodNamesNonKeyElementWithIdVariant_Throws(
        string idAttribute)
    {
        var envelope = BuildEnvelope(
            BuildRetrievalMethodAssertion("#_other", $"<xenc:EncryptedData {idAttribute}=\"_other\" />"));

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    [Theory]
    [InlineData("https://example.com/key")]
    [InlineData("#xpointer(id('_key'))")]
    [InlineData("#")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("#1invalid")]
    [InlineData("#_missing")]
    public void EnsureAcceptedKeyTransportAlgorithms_RetrievalMethodWithUnacceptedUri_Throws(string? uri)
    {
        var envelope = BuildEnvelope(BuildRetrievalMethodAssertion(uri, "<xenc:EncryptedData Id=\"_data\" />"));

        Assert.Throws<Exception>(
            () => Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope));
    }

    private static string BuildRetrievalMethodAssertion(string? uri, string otherElement)
    {
        var uriAttribute = uri == null ? string.Empty : $" URI=\"{EscapeAttributeValue(uri)}\"";
        return "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey Id=\"_key\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            $"<ds:RetrievalMethod{uriAttribute} Type=\"http://www.w3.org/2001/04/xmlenc#EncryptedKey\" />" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            otherElement +
            "</saml:EncryptedAssertion>";
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
