using System.Diagnostics.Metrics;
using System.Xml;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Sustainsys.Saml2;

namespace Bit.SSO.Test.Utilities;

public class Saml2EncryptedAssertionInspectorTests
{
    private const string MeterName = "Bitwarden.Sso.Saml2";
    private const string InstrumentName = "bitwarden.sso.saml2.unsupported_key_transport_algorithm";
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaepMgf1P = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";
    private const string Aes256Cbc = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_PlaintextAssertion_RecordsNoMeasurement()
    {
        var envelope = BuildEnvelope("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>");
        var (context, collector) = BuildContext();

        var result = Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        Assert.True(result);
        // An envelope with no encrypted assertion names no algorithm, so no measurement is recorded.
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Theory]
    [InlineData(RsaOaepMgf1P)]
    [InlineData(RsaOaep)]
    public void TryRecordUnsupportedKeyTransportAlgorithms_NestedEncryptedKeyWithAcceptedAlgorithm_RecordsNoMeasurement(string algorithm)
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(algorithm));
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_NestedEncryptedKeyWithUnacceptedAlgorithm_RecordsMeasurement()
    {
        var envelope = BuildEnvelope(BuildNestedEncryptedAssertion(RsaPkcs1));
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_EncryptedKeyBesideEncryptedData_RecordsMeasurement()
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_DataAndKeyEncryptionMethods_RecordsKeyEncryptionAlgorithm()
    {
        // xenc:EncryptedData names the data encryption algorithm, such as aes256-cbc.
        // xenc:EncryptedKey names the key encryption algorithm.
        // The inspector must record the key encryption algorithm, not the data encryption algorithm.
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        var algorithm = GetAlgorithmTag(measurement);
        Assert.Equal(RsaPkcs1, algorithm);
        Assert.NotEqual(Aes256Cbc, algorithm);
    }

    /// <summary>
    /// An unstated out-of-band agreement is SAML spec-compliant, but rare in practice.
    /// IdPs will generally send the algorithm with the assertion request, so this is an edge case.
    /// </summary>
    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_NoEncryptedKey_RecordsNoneMeasurement()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData>" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>");
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        // One measurement is recorded, because the assertion names no algorithm and a missing algorithm is unaccepted.
        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("none", GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_EncryptionMethodWithoutAlgorithmAttribute_RecordsNoneMeasurement()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("none", GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_EmptyAlgorithmAttribute_RecordsNoneMeasurement()
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey><xenc:EncryptionMethod Algorithm=\"\" /></xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("none", GetAlgorithmTag(measurement));
    }

    [Theory]
    [InlineData("http://www.w3.org/2001/04/xmlenc#kw-aes256")]
    [InlineData("urn:example:unknown-algorithm")]
    [InlineData("rsa-1_5\nlevel=something-else")]
    public void TryRecordUnsupportedKeyTransportAlgorithms_AlgorithmOutsideKnownValues_RecordsUnrecognizedMeasurement(string algorithm)
    {
        var envelope = BuildEnvelope(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{EscapeAttributeValue(algorithm)}\" />" +
            "</xenc:EncryptedKey>" +
            "</saml:EncryptedAssertion>");
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("unrecognized", GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_TwoAssertionsWithDifferentUnacceptedAlgorithms_RecordsTwoMeasurementsInOrder()
    {
        // A federation proxy can aggregate assertions from two identity providers.
        // Each assertion then holds its own key, and the two keys can use different algorithms.
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaPkcs1) +
            BuildNestedEncryptedAssertion("urn:example:unknown-algorithm"));
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurements = collector.GetMeasurementSnapshot();
        Assert.Equal(2, measurements.Count);
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurements[0]));
        Assert.Equal("unrecognized", GetAlgorithmTag(measurements[1]));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_SameUnacceptedAlgorithmInTwoAssertions_RecordsOneMeasurement()
    {
        // Two assertions can share one unaccepted algorithm, such as after a federation proxy
        // aggregates assertions from two identity providers with the same configuration.
        // The inspector must record one measurement, not one measurement for each assertion.
        var envelope = BuildEnvelope(
            BuildNestedEncryptedAssertion(RsaPkcs1) +
            BuildNestedEncryptedAssertion(RsaPkcs1));
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_NullAndAcceptedAlgorithmsInThreeAssertions_RecordsOneNoneMeasurement()
    {
        // The first and the third assertion name no algorithm. Both resolve to null, and null deduplicates.
        // The second assertion uses an accepted algorithm, so it records no measurement.
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal("none", GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_AcceptedKeyBeforeUnacceptedKeyInOneAssertion_RecordsUnacceptedMeasurement()
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        // The accepted algorithm records no measurement, so rsa-1_5 is the only measurement.
        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_TwoUnacceptedKeysInOneAssertion_RecordsTwoMeasurementsInOrder()
    {
        // XML Encryption 1.1 section 3.5.3 states that sibling keys carry the same key value,
        // "possibly encrypted in different ways or for different recipients".
        // Each distinct unaccepted algorithm must reach the metric.
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurements = collector.GetMeasurementSnapshot();
        Assert.Equal(2, measurements.Count);
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurements[0]));
        Assert.Equal("unrecognized", GetAlgorithmTag(measurements[1]));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_SameUnacceptedAlgorithmInTwoKeysOfOneAssertion_RecordsOneMeasurement()
    {
        // An identity provider can send one key for each service provider decryption certificate.
        // Both keys then name the same algorithm, and Distinct keeps the metric volume at one measurement.
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(RsaPkcs1, GetAlgorithmTag(measurement));
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_TwoAcceptedKeysInOneAssertion_RecordsNoMeasurement()
    {
        // Service provider metadata advertises rsa-oaep-mgf1p and rsa-oaep, so an identity provider
        // can send one key for each advertised method. Neither key is unaccepted, so nothing is recorded.
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
        var (context, collector) = BuildContext();

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public void TryRecordUnsupportedKeyTransportAlgorithms_NullEnvelope_ReturnsFalseWithoutRecording()
    {
        var (context, collector) = BuildContext();

        var result = Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(null!, context);

        Assert.False(result);
        Assert.Empty(collector.GetMeasurementSnapshot());
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

    private static (DefaultHttpContext Context, MetricCollector<long> Collector) BuildContext()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<Saml2AssertionMetrics>();
        var provider = services.BuildServiceProvider();

        var collector = new MetricCollector<long>(
            provider.GetRequiredService<IMeterFactory>(), MeterName, InstrumentName);
        var context = new DefaultHttpContext { RequestServices = provider };
        return (context, collector);
    }

    private static string EscapeAttributeValue(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;").Replace("\n", "&#10;");

    private static object? GetAlgorithmTag(CollectedMeasurement<long> measurement) =>
        measurement.Tags["algorithm"];
}
