using System.Diagnostics.Metrics;
using System.Text;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;

namespace Bit.SSO.Test.Utilities;

public class Saml2OptionsExtensionsTests
{
    // The scheme carries the organization ID on this request path.
    // The metric never receives this value, so no measurement carries an organization identifier.
    private const string Scheme = "test-scheme";
    private const string ModulePath = "/saml2/test-scheme";
    private const string IdpEntityId = "https://idp.example.com/metadata";
    private const string MeterName = "Bitwarden.Sso.Saml2";
    private const string InstrumentName = "bitwarden.sso.saml2.unsupported_key_transport_algorithm";
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    [Fact]
    public async Task CouldHandleAsync_NoAssertionAndWantAssertionsSigned_Throws()
    {
        // An envelope with no <saml:Assertion> element must still cause a throw from the
        // signature check. The algorithm validation try/catch wraps only the validation,
        // so it must not hide this throw.
        var options = BuildOptions(wantAssertionsSigned: true);
        using var testContext = BuildPostContext(BuildResponseXml(string.Empty));
        var (context, collector) = testContext;

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_EncryptedAssertionWithOneUnsupportedAlgorithm_RecordsOneMeasurement()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        using var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(RsaPkcs1, measurement.Tags["algorithm"]);
    }

    [Fact]
    public async Task CouldHandleAsync_PlaintextAssertion_RecordsNoMeasurement()
    {
        // An envelope with no encrypted assertion names no key encryption algorithm,
        // so the inspector records no measurement.
        var options = BuildOptions(wantAssertionsSigned: false);
        using var testContext = BuildPostContext(
            BuildResponseXml("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>"));
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_TwoEncryptedAssertionsWithOneUnsupportedAlgorithm_RecordsOneMeasurement()
    {
        // A federation proxy can aggregate assertions from two identity providers.
        // The inspector then records one measurement for each distinct unaccepted algorithm.
        var options = BuildOptions(wantAssertionsSigned: false);
        using var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1) + BuildEncryptedAssertion(RsaOaep)));
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));

        var measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(RsaPkcs1, measurement.Tags["algorithm"]);
    }

    [Fact]
    public async Task CouldHandleAsync_AlgorithmInspectionThrows_DoesNotPropagate()
    {
        // An empty service provider makes the metrics resolution throw.
        // The inspection must swallow that throw, and the login must continue.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildRawPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        Assert.True(await options.CouldHandleAsync(Scheme, context));
    }

    private static Saml2Options BuildOptions(bool wantAssertionsSigned)
    {
        var spOptions = new SPOptions
        {
            EntityId = new EntityId("https://sso.bitwarden.com" + ModulePath),
            ModulePath = ModulePath,
            WantAssertionsSigned = wantAssertionsSigned,
        };
        // This test does not configure a signing certificate.
        // No test case calls XmlHelpers.IsSignedByAny.
        // If the test sets LoadMetadata to true, IdentityProvider.Validate() then requires a certificate.
        var idp = new IdentityProvider(new EntityId(IdpEntityId), spOptions)
        {
            Binding = Saml2BindingType.HttpPost,
            SingleSignOnServiceUrl = new Uri("https://idp.example.com/sso"),
        };

        var options = new Saml2Options { SPOptions = spOptions };
        options.IdentityProviders.Add(idp);
        return options;
    }

    private static string BuildResponseXml(string assertionElement) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

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

    private static DefaultHttpContext BuildRawPostContext(string responseXml)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });
        return context;
    }

    // CouldHandleAsync resolves the inspector metrics from the request services.
    private static MetricTestContext BuildPostContext(string responseXml)
    {
        var context = BuildRawPostContext(responseXml);

        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<Saml2AssertionMetrics>();
        var provider = services.BuildServiceProvider();

        var collector = new MetricCollector<long>(
            provider.GetRequiredService<IMeterFactory>(), MeterName, InstrumentName);
        context.RequestServices = provider;
        return new MetricTestContext(context, collector);
    }

    // Disposing this disposes the collector's underlying listener, so a test does not leak it.
    private sealed record MetricTestContext(DefaultHttpContext Context, MetricCollector<long> Collector) : IDisposable
    {
        public void Dispose() => Collector.Dispose();
    }
}
