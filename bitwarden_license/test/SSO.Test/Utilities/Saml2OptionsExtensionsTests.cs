using System.Text;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
    private const string Scheme = "test-scheme";
    private const string ModulePath = "/saml2/test-scheme";
    private const string IdpEntityId = "https://idp.example.com/metadata";
    private const string RsaPkcs1 = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";

    [Fact]
    public async Task CouldHandleAsync_NoAssertionAndWantAssertionsSigned_Throws()
    {
        // An envelope with no <saml:Assertion> element must still cause a throw from the
        // signature check. The algorithm validation try/catch wraps only the validation,
        // so it must not hide this throw.
        var options = BuildOptions(wantAssertionsSigned: true);
        var logger = new FakeLogger<Saml2Options>();
        var context = BuildPostContext(BuildResponseXml(string.Empty), logger);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_EncryptedAssertionWithOneUnsupportedAlgorithm_LogsOneEntry()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var logger = new FakeLogger<Saml2Options>();
        var context = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)),
            logger);

        Assert.True(await options.CouldHandleAsync(Scheme, context));

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(Scheme, GetStructuredValue(record, "Scheme"));
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public async Task CouldHandleAsync_PlaintextAssertion_LogsNoEntry()
    {
        // An envelope with no encrypted assertion names no key encryption algorithm,
        // so the inspector logs no entry.
        var options = BuildOptions(wantAssertionsSigned: false);
        var logger = new FakeLogger<Saml2Options>();
        var context = BuildPostContext(
            BuildResponseXml("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>"),
            logger);

        Assert.True(await options.CouldHandleAsync(Scheme, context));
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task CouldHandleAsync_TwoEncryptedAssertionsWithOneUnsupportedAlgorithm_LogsOneEntry()
    {
        // A federation proxy can aggregate assertions from two identity providers.
        // The inspector then logs one entry for each distinct unaccepted algorithm.
        var options = BuildOptions(wantAssertionsSigned: false);
        var logger = new FakeLogger<Saml2Options>();
        var context = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1) + BuildEncryptedAssertion(RsaOaep)),
            logger);

        Assert.True(await options.CouldHandleAsync(Scheme, context));

        var records = logger.Collector.GetSnapshot();
        Assert.Single(records);
        var record = records.Single();
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(Scheme, GetStructuredValue(record, "Scheme"));
        Assert.Equal(RsaPkcs1, GetStructuredValue(record, "KeyEncryptionAlgorithm"));
    }

    [Fact]
    public async Task CouldHandleAsync_AlgorithmInspectionThrows_DoesNotPropagate()
    {
        // An empty service provider makes the logger resolution throw.
        // The inspection must swallow that throw, and the login must continue.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));
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

    private static DefaultHttpContext BuildPostContext(string responseXml, ILogger<Saml2Options>? logger = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });

        // CouldHandleAsync resolves the inspector logger from the request services.
        if (logger != null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(logger);
            context.RequestServices = services.BuildServiceProvider();
        }

        return context;
    }

    private static string? GetStructuredValue(FakeLogRecord record, string key) =>
        Assert.Single(record.StructuredState!, entry => entry.Key == key).Value;
}
