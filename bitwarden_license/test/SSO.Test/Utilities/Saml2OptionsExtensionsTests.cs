using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
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
using CipherData = System.Security.Cryptography.Xml.CipherData;
using EncryptedData = System.Security.Cryptography.Xml.EncryptedData;
using EncryptedKey = System.Security.Cryptography.Xml.EncryptedKey;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;
using EncryptionMethod = System.Security.Cryptography.Xml.EncryptionMethod;
using KeyInfo = System.Security.Cryptography.Xml.KeyInfo;
using KeyInfoEncryptedKey = System.Security.Cryptography.Xml.KeyInfoEncryptedKey;

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
    public async Task CouldHandleAsync_EncryptedAndSignedAssertionAndWantAssertionsSigned_DoesNotThrow()
    {
        // The identity provider signs the assertion, then encrypts the whole thing, exactly like a
        // real round trip. The pre-flight check must decrypt before it can see the signature.
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedAssertion = BuildSignedAssertion(signingCertificate);
        var encryptedAssertionXml = EncryptAssertion(signedAssertion, decryptionCertificate);

        using var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml));
        var (context, collector) = testContext;

        Assert.True(await options.CouldHandleAsync(Scheme, context));
    }

    [Fact]
    public async Task CouldHandleAsync_EncryptedButUnsignedAssertionAndWantAssertionsSigned_Throws()
    {
        // Decryption succeeding is not proof of a valid signature. An assertion that decrypts
        // cleanly but was never signed must still be rejected.
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        var unsignedAssertion = BuildAssertionDocument().DocumentElement!;
        var encryptedAssertionXml = EncryptAssertion(unsignedAssertion, decryptionCertificate);

        using var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml));
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
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        const string unsignedPlaintextAssertion =
            "<saml:Assertion ID=\"_plaintext\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>";
        var encryptedAssertionXml = EncryptAssertion(BuildAssertionDocument().DocumentElement!, decryptionCertificate);

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
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedPlaintextAssertion = BuildSignedAssertion(signingCertificate);
        var unsignedEncryptedAssertionXml =
            EncryptAssertion(BuildAssertionDocument().DocumentElement!, decryptionCertificate);

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
        var signingCertificate = CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);

        var signedPlaintextAssertion = BuildSignedAssertion(signingCertificate);
        var signedEncryptedAssertionXml =
            EncryptAssertion(BuildSignedAssertion(signingCertificate), decryptionCertificate);

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

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));
    }

    private static XmlDocument BuildAssertionDocument() =>
        XmlHelpers.XmlDocumentFromString(
            "<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_assertion\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "</saml:Assertion>");

    private static XmlElement BuildSignedAssertion(X509Certificate2 signingCertificate)
    {
        var document = BuildAssertionDocument();
        document.DocumentElement!.Sign(signingCertificate, includeKeyInfo: false);
        return document.DocumentElement!;
    }

    // Encrypts an assertion element the same way a real IdP does: an AES content key wraps the
    // assertion, and the SP's certificate wraps that key. This is real XML encryption, not a fixed
    // fixture, so it exercises the same decrypt path Sustainsys.Saml2 uses in production.
    private static string EncryptAssertion(XmlElement assertion, X509Certificate2 encryptionCertificate)
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
