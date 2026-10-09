using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bit.Core;
using Bit.Core.Settings;
using Bit.Sso.Utilities.Saml2;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
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
    private static readonly Guid OrganizationId = Guid.Parse("5b6f7c1e-2d3a-4e8f-9a10-0c7d2f4e6b81");
    private static readonly string Scheme = OrganizationId.ToString();
    private static readonly string ModulePath = $"/saml2/{Scheme}";
    private const string IdpEntityId = "https://idp.example.com/metadata";
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
        var testContext = BuildPostContext(BuildResponseXml(string.Empty), featureFlagEnabled);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
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

        var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
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

        var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
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

        var testContext = BuildPostContext(
            BuildResponseXml(unsignedPlaintextAssertion + encryptedAssertionXml));

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
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

        var testContext = BuildPostContext(
            BuildResponseXml(signedPlaintextAssertion.OuterXml + unsignedEncryptedAssertionXml));

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
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

        var testContext = BuildPostContext(
            BuildResponseXml(signedPlaintextAssertion.OuterXml + signedEncryptedAssertionXml));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15CloudWithFlagOn_QueuesNoticeForTheSchemeOrganization()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailFlagEnabled: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15CloudWithFlagOff_DoesNotQueueNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailFlagEnabled: false);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15SelfHostedWithFlagOff_QueuesNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), selfHosted: true, rsa15EmailFlagEnabled: false);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_OaepAssertion_DoesNotQueueNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaOaep)), rsa15EmailFlagEnabled: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_PlaintextAssertion_DoesNotQueueNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml("<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>"),
            rsa15EmailFlagEnabled: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_SchemeIsNotAGuid_DoesNotQueueNotice()
    {
        // The module path check reads options.SPOptions.ModulePath, never the scheme argument.
        // The options keep the Guid module path, so the request passes the path check.
        // The scheme argument alone is not a Guid, and the query scheme does not match it, so the body is parsed.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailFlagEnabled: true);

        Assert.True(await options.CouldHandleAsync("test-scheme", testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_NotifierThrows_StillReturnsTrue()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailFlagEnabled: true);
        testContext.Notifier.When(n => n.TryQueue(Arg.Any<Guid>()))
            .Do(_ => throw new InvalidOperationException("notifier failure"));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15WithUnsignedAssertion_StillThrowsSignatureError()
    {
        // The notice call precedes the signature check, so the notice is queued for the unsigned assertion,
        // and the signature check then still throws.
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        var unsignedAssertion = Saml2TestXml.BuildAssertionDocument().DocumentElement!;
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            unsignedAssertion.OuterXml, decryptionCertificate, EncryptedXml.XmlEncRSA15Url);

        var testContext = BuildPostContext(
            BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true, rsa15EmailFlagEnabled: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
        testContext.Notifier.Received(1).TryQueue(OrganizationId);
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

    // CouldHandleAsync resolves the PM42982_WantAssertionsSigned feature flag (when WantAssertionsSigned is true)
    // from the request services.
    // It also resolves the global settings, the PM43819_Rsa15DeprecationEmail feature flag, and the RSA 1.5 notifier.
    private static PostTestContext BuildPostContext(string responseXml, bool featureFlagEnabled = true,
        bool selfHosted = false, bool rsa15EmailFlagEnabled = false)
    {
        var context = BuildRawPostContext(responseXml);

        var services = new ServiceCollection();

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM42982_WantAssertionsSigned).Returns(featureFlagEnabled);
        featureService.IsEnabled(FeatureFlagKeys.PM43819_Rsa15DeprecationEmail).Returns(rsa15EmailFlagEnabled);
        services.AddSingleton(featureService);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.SelfHosted.Returns(selfHosted);
        services.AddSingleton(globalSettings);

        var notifier = Substitute.For<ISaml2Rsa15DeprecationNotifier>();
        services.AddSingleton(notifier);

        context.RequestServices = services.BuildServiceProvider();
        return new PostTestContext(context, notifier);
    }

    private sealed record PostTestContext(DefaultHttpContext Context, ISaml2Rsa15DeprecationNotifier Notifier);

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
