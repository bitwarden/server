using System.Xml;
using Bit.Sso.Utilities;
using Sustainsys.Saml2;

namespace Bit.SSO.Test.Utilities;

public class Saml2EncryptedAssertionInspectorTests
{
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaepMgf1P = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";
    private const string Aes256Cbc = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";

    [Fact]
    public void InspectKeyEncryptionAlgorithms_PlaintextAssertion_ReturnsEmptyList()
    {
        var envelope = BuildEnvelope("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        // An empty list reports that the envelope carries no encrypted assertion.
        Assert.Empty(algorithms);
    }

    [Theory]
    [InlineData(RsaPkcs1)]
    [InlineData(RsaOaepMgf1P)]
    [InlineData(RsaOaep)]
    public void InspectKeyEncryptionAlgorithms_NestedEncryptedKeyWithKnownAlgorithm_ReturnsAlgorithm(string algorithm)
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{algorithm}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal(algorithm, Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_EncryptedKeyBesideEncryptedData_ReturnsAlgorithm()
    {
        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData.
        // An xenc:ReferenceList links the key to the data.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey Id=\"_key\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "<xenc:ReferenceList><xenc:DataReference URI=\"#_data\" /></xenc:ReferenceList>" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedData Id=\"_data\">" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal(RsaOaepMgf1P, Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_DataAndKeyEncryptionMethods_ReturnsKeyEncryptionAlgorithm()
    {
        // xenc:EncryptedData names the data encryption algorithm, such as aes256-cbc.
        // xenc:EncryptedKey names the key encryption algorithm.
        // The inspector must return the key encryption algorithm.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            $"<xenc:EncryptionMethod Algorithm=\"{Aes256Cbc}\" />" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        var algorithm = Assert.Single(algorithms);
        Assert.Equal(RsaPkcs1, algorithm);
        Assert.NotEqual(Aes256Cbc, algorithm);
    }

    /// <summary>
    /// An unstated out-of-band agreement is SAML spec-compliant, but rare in practice.
    /// IdPs will generally send the algorithm with the assertion request, so this is an edge case.
    /// </summary>
    [Fact]
    public void InspectKeyEncryptionAlgorithms_NoEncryptedKey_ReturnsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        // The list holds one entry, because the envelope holds an encrypted assertion.
        // The entry is null, because the assertion names no algorithm.
        Assert.Null(Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_EncryptionMethodWithoutAlgorithmAttribute_ReturnsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Null(Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_EmptyAlgorithmAttribute_ReturnsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"\" /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Null(Assert.Single(algorithms));
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#kw-aes256")]
    [InlineData("urn:example:unknown-algorithm")]
    [InlineData("rsa-1_5\nlevel=something-else")]
    public void InspectKeyEncryptionAlgorithms_AlgorithmOutsideKnownValues_ReturnsUnrecognized(string algorithm)
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{EscapeAttributeValue(algorithm)}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal("unrecognized", Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_DifferentAlgorithmsInTwoAssertions_ReturnsBothInOrder()
    {
        // A federation proxy can aggregate assertions from two identity providers.
        // Each assertion then holds its own key, and the two keys can use different algorithms.
        var envelope = BuildEnvelope(
            BuildEncryptedAssertion(RsaPkcs1) +
            BuildEncryptedAssertion(RsaOaepMgf1P));

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal(new string?[] { RsaPkcs1, RsaOaepMgf1P }, algorithms);
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_SameAlgorithmInTwoAssertions_ReturnsOneEntry()
    {
        var envelope = BuildEnvelope(
            BuildEncryptedAssertion(RsaPkcs1) +
            BuildEncryptedAssertion(RsaPkcs1));

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal(RsaPkcs1, Assert.Single(algorithms));
    }

    [Fact]
    public void InspectKeyEncryptionAlgorithms_NullAndKnownAlgorithmsInThreeAssertions_ReturnsTwoEntries()
    {
        // The first and the third assertion name no algorithm. Both resolve to null, and null deduplicates.
        // Null stays a distinct value from the algorithm of the second assertion.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>" +
            BuildEncryptedAssertion(RsaOaep) +
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");

        var algorithms = Saml2EncryptedAssertionInspector.InspectKeyEncryptionAlgorithms(envelope);

        Assert.Equal(new string?[] { null, RsaOaep }, algorithms);
    }

    private static string BuildEncryptedAssertion(string algorithm) =>
        "<saml:EncryptedAssertion>" +
        "<xenc:EncryptedKey>" +
        $"<xenc:EncryptionMethod Algorithm=\"{algorithm}\" />" +
        "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
        "</xenc:EncryptedKey>" +
        "<xenc:EncryptedData>" +
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
