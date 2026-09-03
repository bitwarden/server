using System.Xml;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.SSO.Test.Utilities;

public class Saml2EncryptedAssertionInspectorTests
{
    private const string Scheme = "test-scheme";
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaepMgf1P = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";
    private const string Aes256Cbc = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_PlaintextAssertion_LogsNoEntry()
    {
        var envelope = BuildEnvelope("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");
        var (context, logger) = BuildContext();

        var result = Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        Assert.True(result);
        // An envelope with no encrypted assertion names no algorithm, so no entry is logged.
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Theory]
    [InlineData(RsaOaepMgf1P)]
    [InlineData(RsaOaep)]
    public void TryLogUnsupportedKeyTransportAlgorithms_NestedEncryptedKeyWithAcceptedAlgorithm_LogsNoEntry(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_NestedEncryptedKeyWithUnacceptedAlgorithm_LogsEntry()
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(RsaPkcs1));
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(Scheme, GetStructuredValue(record, "Scheme"));
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_EncryptedKeyBesideEncryptedData_LogsEntry()
    {
        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData.
        // An xenc:ReferenceList links the key to the data.
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
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_DataAndKeyEncryptionMethods_LogsKeyEncryptionAlgorithm()
    {
        // xenc:EncryptedData names the data encryption algorithm, such as aes256-cbc.
        // xenc:EncryptedKey names the key encryption algorithm.
        // The inspector must log the key encryption algorithm, not the data encryption algorithm.
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
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        var algorithm = GetStructuredValue(record, "KeyEncryptionAlgorithm");
        Assert.Equal(RsaPkcs1, algorithm);
        Assert.NotEqual(Aes256Cbc, algorithm);
    }

    /// <summary>
    /// An unstated out-of-band agreement is SAML spec-compliant, but rare in practice.
    /// IdPs will generally send the algorithm with the assertion request, so this is an edge case.
    /// </summary>
    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_NoEncryptedKey_LogsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        // One entry is logged, because the assertion names no algorithm and a missing algorithm is unaccepted.
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Null(GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_EncryptionMethodWithoutAlgorithmAttribute_LogsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Null(GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_EmptyAlgorithmAttribute_LogsNullEntry()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"\" /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Null(GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#kw-aes256")]
    [InlineData("urn:example:unknown-algorithm")]
    [InlineData("rsa-1_5\nlevel=something-else")]
    public void TryLogUnsupportedKeyTransportAlgorithms_AlgorithmOutsideKnownValues_LogsUnrecognizedEntry(string algorithm)
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{EscapeAttributeValue(algorithm)}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal("unrecognized", GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_TwoAssertionsWithDifferentUnacceptedAlgorithms_LogsTwoEntriesInOrder()
    {
        // A federation proxy can aggregate assertions from two identity providers.
        // Each assertion then holds its own key, and the two keys can use different algorithms.
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaPkcs1) +
            BuildNestedEncryptedAssertion("urn:example:unknown-algorithm"));
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var records = logger.Collector.GetSnapshot();
        Assert.Equal(2, records.Count);
        Assert.Equal(RsaPkcs1, GetStructuredValue(records[0], "KeyEncryptionAlgorithm"));
        Assert.Equal("unrecognized", GetStructuredValue(records[1], "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_SameUnacceptedAlgorithmInTwoAssertions_LogsOneEntry()
    {
        // Two assertions can share one unaccepted algorithm, such as after a federation proxy
        // aggregates assertions from two identity providers with the same configuration.
        // The inspector must log one entry, not one entry for each assertion.
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaPkcs1) +
            BuildNestedEncryptedAssertion(RsaPkcs1));
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_NullAndAcceptedAlgorithmsInThreeAssertions_LogsOneNullEntry()
    {
        // The first and the third assertion name no algorithm. Both resolve to null, and null deduplicates.
        // The second assertion uses an accepted algorithm, so it logs no entry.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>" +
            BuildNestedEncryptedAssertion(RsaOaep) +
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Null(GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_AcceptedKeyBeforeUnacceptedKeyInOneAssertion_LogsUnacceptedEntry()
    {
        // The SAML 2.0 assertion schema declares xenc:EncryptedKey with maxOccurs="unbounded"
        // inside saml:EncryptedElementType, so one assertion can hold more than one key.
        // The first key uses an accepted algorithm and the second uses rsa-1_5.
        // The inspector must read past the first key, otherwise the accepted algorithm hides rsa-1_5.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData Id=\"_data\">" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "<xenc:EncryptedKey Id=\"_key\">" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "<xenc:CipherData><xenc:CipherValue>a2V5</xenc:CipherValue></xenc:CipherData>" +
            "<xenc:ReferenceList><xenc:DataReference URI=\"#_data\" /></xenc:ReferenceList>" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        // The accepted algorithm logs no entry, so rsa-1_5 is the only entry.
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_TwoUnacceptedKeysInOneAssertion_LogsTwoEntriesInOrder()
    {
        // XML Encryption 1.1 section 3.5.3 states that sibling keys carry the same key value,
        // "possibly encrypted in different ways or for different recipients".
        // Each distinct unaccepted algorithm must reach the log.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedKey>" +
            "<xenc:EncryptionMethod Algorithm=\"urn:example:unknown-algorithm\" />" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var records = logger.Collector.GetSnapshot();
        Assert.Equal(2, records.Count);
        Assert.Equal(RsaPkcs1, GetStructuredValue(records[0], "KeyEncryptionAlgorithm"));
        Assert.Equal("unrecognized", GetStructuredValue(records[1], "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_SameUnacceptedAlgorithmInTwoKeysOfOneAssertion_LogsOneEntry()
    {
        // An identity provider can send one key for each service provider decryption certificate.
        // Both keys then name the same algorithm, and Distinct keeps the log volume at one entry.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_TwoAcceptedKeysInOneAssertion_LogsNoEntry()
    {
        // Service provider metadata advertises rsa-oaep-mgf1p and rsa-oaep, so an identity provider
        // can send one key for each advertised method. Neither key is unaccepted, so nothing is logged.
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<ds:KeyInfo>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaepMgf1P}\" />" +
            "</xenc:EncryptedKey>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaOaep}\" />" +
            "</xenc:EncryptedKey>" +
            "</ds:KeyInfo>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");
        var (context, logger) = BuildContext();

        Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(envelope, Scheme, context);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public void TryLogUnsupportedKeyTransportAlgorithms_NullEnvelope_ReturnsFalseWithoutThrowing()
    {
        var (context, logger) = BuildContext();

        var result = Saml2EncryptedAssertionInspector.TryLogUnsupportedKeyTransportAlgorithms(null!, Scheme, context);

        Assert.False(result);
        Assert.Empty(logger.Collector.GetSnapshot());
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

    private static (DefaultHttpContext Context, FakeLogger<Saml2Options> Logger) BuildContext()
    {
        var logger = new FakeLogger<Saml2Options>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<Saml2Options>>(logger);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return (context, logger);
    }

    private static string EscapeAttributeValue(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;").Replace("\n", "&#10;");

    private static string? GetStructuredValue(FakeLogRecord record, string key) =>
        Assert.Single(record.StructuredState!, entry => entry.Key == key).Value;
}
