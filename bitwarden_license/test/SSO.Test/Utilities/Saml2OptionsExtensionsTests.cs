using System.Diagnostics.Metrics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core;
using Bit.Sso.Utilities.Saml2;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;

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
    public async Task CouldHandleAsync_PathOutsideModulePath_ReturnsFalse()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildRawPostContext("SAMLResponse", EncodeBase64(BuildResponseXml(string.Empty)));
        context.Request.Path = "/unrelated/" + Scheme + "/Acs";

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    [Fact]
    public async Task CouldHandleAsync_NoDefaultIdentityProvider_ReturnsFalse()
    {
        var options = new Saml2Options
        {
            SPOptions = new SPOptions
            {
                EntityId = new EntityId("https://sso.bitwarden.com" + ModulePath),
                ModulePath = ModulePath,
            },
        };
        var context = BuildRawPostContext("SAMLResponse", EncodeBase64(BuildResponseXml(string.Empty)));

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    /// <summary>
    /// When the request path is under the module path, a default identity provider exists,
    /// and the query-provided scheme matches the evaluated scheme,
    /// CouldHandleAsync should return true before it reads the body,
    /// even if the body is not in an expected format.
    /// </summary>
    /// <seealso cref="CouldHandleAsync_SchemeQueryDoesNotMatchAndBodyIsInvalid_ParsesBodyAndReturnsFalse"/>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CouldHandleAsync_SchemeQueryMatches_ReturnsTrueWithoutParsingBody(bool wantAssertionsSigned)
    {
        // Scheme matching behavior holds regardless of assertion signature enforcement.
        var options = BuildOptions(wantAssertionsSigned: wantAssertionsSigned);
        // control: A not-base64-encoded body should return false when Scheme does not match.
        var context = BuildRawPostContext("SAMLResponse", "not-base64-at-all!!");
        context.Request.QueryString = QueryString.Create("scheme", Scheme);
        var formFeature = SpyOnForm(context);

        Assert.True(await options.CouldHandleAsync(Scheme, context));
        Assert.False(WasFormRead(formFeature));
    }

    /// <summary>
    /// When the query-provided scheme does not match the expected value,
    /// CouldHandleAsync should parse the request body.
    /// When the body is not in an expected format, it should return false.
    /// </summary>
    /// <seealso cref="CouldHandleAsync_SchemeQueryMatches_ReturnsTrueWithoutParsingBody"/>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CouldHandleAsync_SchemeQueryDoesNotMatchAndBodyIsInvalid_ParsesBodyAndReturnsFalse(bool wantAssertionsSigned)
    {
        // Scheme matching behavior holds regardless of assertion signature enforcement.
        var options = BuildOptions(wantAssertionsSigned: wantAssertionsSigned);
        var context = BuildRawPostContext("SAMLResponse", "not-base64-at-all!!");
        context.Request.QueryString = QueryString.Create("scheme", "0d3c9a4e-7f21-4b6a-8e5d-2c1f9b7a6e30");
        var formFeature = SpyOnForm(context);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
        Assert.True(WasFormRead(formFeature));
    }

    [Theory]
    [InlineData("SAMLResponse", "", true)]
    [InlineData("SAMLResponse", "   ", true)]
    [InlineData("SAMLRequest", "", true)]
    [InlineData("SAMLRequest", "    ", true)]
    [InlineData("SAMLResponse", "   ", false)]
    [InlineData("SAMLResponse", "", false)]
    [InlineData("SAMLRequest", "", false)]
    [InlineData("SAMLRequest", "   ", false)]
    public async Task CouldHandleAsync_BlankSamlMessage_ReturnsFalse(string formField, string value, bool wantAssertionsSigned)
    {
        var options = BuildOptions(wantAssertionsSigned: wantAssertionsSigned);
        var context = BuildRawPostContext(formField, value);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    [Theory]
    [InlineData("not-base64-at-all!!")]
    [InlineData("bm90IGRlZmxhdGVkIGRhdGE=")]
    public async Task CouldHandleAsync_GetPayloadIsNotValidDeflate_ReturnsFalse(string payload)
    {
        // The first payload is not Base64, which raises the FormatException branch.
        // The second payload is Base64 of bytes that are not a deflate stream.
        // Both decline the scheme quietly.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildGetContext("SAMLRequest", payload);
        // The scheme check reads the query on every request. Only the GET branch reads SAMLRequest.
        var query = Substitute.For<IQueryCollection>();
        query["SAMLRequest"].Returns(new StringValues(payload));
        var queryFeature = Substitute.For<IQueryFeature>();
        queryFeature.Query.Returns(query);
        context.Features.Set(queryFeature);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
        _ = query.Received()["SAMLRequest"];
    }

    [Fact]
    public async Task CouldHandleAsync_PayloadWithoutSaml2Envelope_ReturnsFalse()
    {
        // Only a form POST or a GET carries a SAML message. Any other request leaves the envelope null.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.ContentType = "application/json";

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CouldHandleAsync_NoAssertionAndWantAssertionsSigned_Throws(bool featureFlagEnabled)
    {
        // An envelope with no <saml:Assertion> element must still cause a throw from the
        // signature check. The algorithm validation try/catch wraps only the validation,
        // so it must not hide this throw.
        // The throw must occur in both the flag-on and the legacy flag-off signature checks.
        var options = BuildOptions(wantAssertionsSigned: true);
        using var testContext = BuildPostContext(BuildResponseXml(string.Empty), featureFlagEnabled);
        var (context, collector) = testContext;

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Theory]
    [InlineData(EncryptedXml.XmlEncRSAOAEPUrl)]
    [InlineData(EncryptedXml.XmlEncRSA15Url)]
    public async Task CouldHandleAsync_EncryptedAndSignedAssertionAndWantAssertionsSigned_DoesNotThrow(
        string keyTransportAlgorithm)
    {
        // The identity provider signs the assertion, then encrypts the whole thing, exactly like a
        // real round trip. The pre-flight check must decrypt before it can see the signature.
        var signingCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedAssertion = Saml2TestXml.BuildSignedAssertion(signingCertificate);
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            signedAssertion.OuterXml, decryptionCertificate, keyTransportAlgorithm);

        using var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true);
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));
    }

    [Theory]
    [InlineData(EncryptedXml.XmlEncRSAOAEPUrl)]
    [InlineData(EncryptedXml.XmlEncRSA15Url)]
    public async Task CouldHandleAsync_EncryptedButUnsignedAssertionAndWantAssertionsSigned_Throws(
        string keyTransportAlgorithm)
    {
        // Decryption succeeding is not proof of a valid signature. An assertion that decrypts
        // cleanly but was never signed must still be rejected.
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        var unsignedAssertion = Saml2TestXml.BuildAssertionDocument().DocumentElement!;
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            unsignedAssertion.OuterXml, decryptionCertificate, keyTransportAlgorithm);

        using var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true);
        var (context, collector) = testContext;

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_MixedPlaintextAndEncryptedAssertionAndWantAssertionsSigned_ChecksPlaintextAssertion()
    {
        // A response can carry a plaintext assertion alongside a separate encrypted one (e.g. a
        // federation proxy). The unsigned plaintext assertion must still be checked and rejected,
        // regardless of its encrypted sibling.
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        const string unsignedPlaintextAssertion =
            "<saml:Assertion ID=\"_plaintext\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>";
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            Saml2TestXml.BuildAssertionDocument().DocumentElement!.OuterXml, decryptionCertificate);

        using var testContext = BuildPostContext(
            BuildResponseXml(unsignedPlaintextAssertion + encryptedAssertionXml));
        var (context, collector) = testContext;

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SignedPlaintextAssertionWithUnsignedEncryptedSiblingAndWantAssertionsSigned_Throws()
    {
        // A response can carry more than one assertion (e.g. a federation proxy). A validly
        // signed plaintext assertion must not let an unsigned encrypted sibling through unchecked.
        var signingCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedPlaintextAssertion = Saml2TestXml.BuildSignedAssertion(signingCertificate);
        var unsignedEncryptedAssertionXml =
            Saml2TestXml.EncryptAssertion(Saml2TestXml.BuildAssertionDocument().DocumentElement!.OuterXml, decryptionCertificate);

        using var testContext = BuildPostContext(
            BuildResponseXml(signedPlaintextAssertion.OuterXml + unsignedEncryptedAssertionXml));
        var (context, collector) = testContext;

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_TwoSignedAssertionsAndWantAssertionsSigned_DoesNotThrow()
    {
        // A federation proxy can aggregate a signed plaintext assertion and a signed, encrypted
        // one. Both must pass the check independently for the response to be accepted.
        var signingCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedPlaintextAssertion = Saml2TestXml.BuildSignedAssertion(signingCertificate);
        var signedEncryptedAssertionXml =
            Saml2TestXml.EncryptAssertion(Saml2TestXml.BuildSignedAssertion(signingCertificate).OuterXml, decryptionCertificate);

        using var testContext = BuildPostContext(
            BuildResponseXml(signedPlaintextAssertion.OuterXml + signedEncryptedAssertionXml));
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));
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

    private static Saml2Options BuildOptions(bool wantAssertionsSigned,
        X509Certificate2? decryptionCertificate = null, X509Certificate2? signingCertificate = null)
    {
        var spOptions = new SPOptions
        {
            EntityId = new EntityId("https://sso.bitwarden.com" + ModulePath),
            ModulePath = ModulePath,
            WantAssertionsSigned = wantAssertionsSigned,
        };
        if (decryptionCertificate != null)
        {
            spOptions.ServiceCertificates.Add(decryptionCertificate);
        }
        // If a test case does not pass a signing certificate, it must not call
        // XmlHelpers.IsSignedByAny. If the test sets LoadMetadata to true,
        // IdentityProvider.Validate() then requires a certificate.
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

    private static DefaultHttpContext BuildRawPostContext(string responseXml) =>
        BuildRawPostContext("SAMLResponse", EncodeBase64(responseXml));

    private static DefaultHttpContext BuildRawPostContext(string formField, string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [formField] = value,
        });
        return context;
    }

    private static DefaultHttpContext BuildGetContext(string queryField, string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.QueryString = QueryString.Create(queryField, value);
        return context;
    }

    private static string EncodeBase64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    // CouldHandleAsync resolves the inspector metrics, and (when WantAssertionsSigned is true)
    // the PM42982_WantAssertionsSigned feature flag, from the request services.
    private static MetricTestContext BuildPostContext(string responseXml, bool featureFlagEnabled = true)
    {
        var context = BuildRawPostContext(responseXml);

        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<Saml2AssertionMetrics>();

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM42982_WantAssertionsSigned).Returns(featureFlagEnabled);
        services.AddSingleton(featureService);

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

    private static IFormFeature SpyOnForm(HttpContext context)
    {
        var form = context.Request.Form;
        var formFeature = Substitute.For<IFormFeature>();
        formFeature.HasFormContentType.Returns(true);
        formFeature.Form.Returns(form);
        formFeature.ReadForm().Returns(form);
        formFeature.ReadFormAsync(default).ReturnsForAnyArgs(form);
        context.Features.Set(formFeature);
        return formFeature;
    }

    private static bool WasFormRead(IFormFeature formFeature) =>
        formFeature.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name is nameof(IFormFeature.ReadForm) or nameof(IFormFeature.ReadFormAsync));
}
