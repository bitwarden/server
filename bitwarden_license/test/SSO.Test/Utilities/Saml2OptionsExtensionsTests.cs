using System.IO.Compression;
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
    private const string RsaOaepMgf1p = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private const string RsaOaep = "http://www.w3.org/2009/xmlenc11#rsa-oaep";
    private const string PlaintextAssertion = "<saml:Assertion ID=\"_assertion\"><saml:Issuer>idp</saml:Issuer></saml:Assertion>";

    [Fact]
    public async Task CouldHandleAsync_PathOutsideModulePath_ReturnsFalse()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildRawPostContext("SAMLResponse", EncodeBase64(BuildResponseXml(string.Empty)));
        context.Request.Path = "/unrelated/" + Scheme + "/Acs";

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    /// <summary>
    /// The middleware calls CouldHandleAsync for every SAML scheme on every request.
    /// A request outside the module path of the scheme must not read any feature flag.
    /// </summary>
    [Fact]
    public async Task CouldHandleAsync_PathOutsideModulePath_ReturnsFalseWithoutReadingTheFlag()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildRawPostContext("SAMLResponse", EncodeBase64(BuildResponseXml(string.Empty)));
        context.Request.Path = "/unrelated/" + Scheme + "/Acs";
        var featureService = Substitute.For<IFeatureService>();
        context.RequestServices = new ServiceCollection().AddSingleton(featureService).BuildServiceProvider();

        Assert.False(await options.CouldHandleAsync(Scheme, context));
        featureService.DidNotReceiveWithAnyArgs().IsEnabled(default!);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CouldHandleAsync_SchemeQueryMatchesAndBodyIsInvalidAndRsa15DeprecationFlagOn_ParsesBodyAndReturnsFalse(
        bool wantAssertionsSigned)
    {
        // The scheme shortcut applies only to a request without a SAML message. The post binding
        // selects this request, and its unbinding rejects the body.
        var options = BuildOptions(wantAssertionsSigned: wantAssertionsSigned);
        var context = BuildRawPostContext("SAMLResponse", "not-base64-at-all!!", rsa15DeprecationFlag: true);
        context.Request.QueryString = QueryString.Create("scheme", Scheme);
        var formFeature = SpyOnForm(context);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
        Assert.True(WasFormRead(formFeature));
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
    [InlineData("SAMLResponse", "", true, false)]
    [InlineData("SAMLResponse", "   ", true, false)]
    [InlineData("SAMLRequest", "", true, false)]
    [InlineData("SAMLRequest", "    ", true, false)]
    [InlineData("SAMLResponse", "   ", false, false)]
    [InlineData("SAMLResponse", "", false, false)]
    [InlineData("SAMLRequest", "", false, false)]
    [InlineData("SAMLRequest", "   ", false, false)]
    [InlineData("SAMLResponse", "", true, true)]
    [InlineData("SAMLResponse", "   ", true, true)]
    [InlineData("SAMLRequest", "", true, true)]
    [InlineData("SAMLRequest", "    ", true, true)]
    [InlineData("SAMLResponse", "   ", false, true)]
    [InlineData("SAMLResponse", "", false, true)]
    [InlineData("SAMLRequest", "", false, true)]
    [InlineData("SAMLRequest", "   ", false, true)]
    public async Task CouldHandleAsync_BlankSamlMessage_ReturnsFalse(
        string formField, string value, bool wantAssertionsSigned, bool rsa15DeprecationFlag)
    {
        var options = BuildOptions(wantAssertionsSigned: wantAssertionsSigned);
        var context = BuildRawPostContext(formField, value, rsa15DeprecationFlag);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    [Theory]
    [InlineData("not-base64-at-all!!", false)]
    [InlineData("bm90IGRlZmxhdGVkIGRhdGE=", false)]
    [InlineData("not-base64-at-all!!", true)]
    [InlineData("bm90IGRlZmxhdGVkIGRhdGE=", true)]
    public async Task CouldHandleAsync_GetPayloadIsNotValidDeflate_ReturnsFalse(
        string payload, bool rsa15DeprecationFlag)
    {
        // The first payload is not Base64, which raises the FormatException branch.
        // The second payload is Base64 of bytes that are not a deflate stream.
        // Both decline the scheme quietly.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildGetContext("SAMLRequest", payload, rsa15DeprecationFlag);
        // The scheme check reads the query on every request. Only the legacy GET branch reads SAMLRequest.
        var query = Substitute.For<IQueryCollection>();
        query["SAMLRequest"].Returns(new StringValues(payload));
        var queryFeature = Substitute.For<IQueryFeature>();
        queryFeature.Query.Returns(query);
        context.Features.Set(queryFeature);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
        if (!rsa15DeprecationFlag)
        {
            _ = query.Received()["SAMLRequest"];
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_PayloadWithoutSaml2Envelope_ReturnsFalse(bool rsa15DeprecationFlag)
    {
        // Only a form POST or a GET carries a SAML message. Any other request leaves the envelope null.
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildBareContext(HttpMethods.Post, rsa15DeprecationFlag);
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

    [Theory]
    [InlineData(7)]
    [InlineData(1)]
    public async Task CouldHandleAsync_Rsa15WithPositiveCooldown_QueuesNoticeForTheSchemeOrganization(int cooldownInDays)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailCooldownInDays: cooldownInDays);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CouldHandleAsync_Rsa15WithCooldownZeroOrLess_DoesNotQueueNotice(int cooldownInDays)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)), rsa15EmailCooldownInDays: cooldownInDays);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_OaepAssertion_DoesNotQueueNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaOaep)));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_PlaintextAssertion_DoesNotQueueNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(PlaintextAssertion));

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
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));

        Assert.True(await options.CouldHandleAsync("test-scheme", testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_NotifierThrows_StillReturnsTrue()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));
        testContext.Notifier.When(n => n.TryQueue(Arg.Any<Guid>()))
            .Do(_ => throw new InvalidOperationException("notifier failure"));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Theory]
    [InlineData(false, "Cannot verify SAML assertion signature.")]
    [InlineData(true, Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage)]
    public async Task CouldHandleAsync_Rsa15WithUnsignedAssertion_ThrowsTheErrorOfTheFirstCheck(
        bool rsa15DeprecationFlag, string expectedMessage)
    {
        // The notice call precedes the signature check, so the notice is queued for the unsigned assertion.
        // With the flag on, the key transport gate precedes the signature check, which decrypts.
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate);

        var unsignedAssertion = Saml2TestXml.BuildAssertionDocument().DocumentElement!;
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            unsignedAssertion.OuterXml, decryptionCertificate, EncryptedXml.XmlEncRSA15Url);

        var testContext = BuildPostContext(
            BuildResponseXml(encryptedAssertionXml), featureFlagEnabled: true,
            rsa15DeprecationFlag: rsa15DeprecationFlag);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
        Assert.Equal(expectedMessage, exception.Message);
        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithFormAndQueryMessages_InspectsFormMessageOnly()
    {
        // The Sustainsys.Saml2 handler reads the query message first. Current behavior with the flag off;
        // PM-42283 changes this with the flag on.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion));
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1))));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithFormAndQueryMessagesAndRsa15DeprecationFlagOn_InspectsQueryMessageOnly()
    {
        // The redirect binding comes first in Saml2Binding.Get and accepts a query message on any method.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion), rsa15DeprecationFlag: true);
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1))));

        // The gate rejects the rsa-1_5 query message after the notice is queued.
        await Assert.ThrowsAsync<Exception>(() => options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    // Current behavior with the flag off; PM-42283 changes this with the flag on.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CouldHandleAsync_PutWithQuerySamlResponse_ReturnsWhetherARedirectBindingReadsIt(
        bool rsa15DeprecationFlag, bool expected)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Method = HttpMethods.Put;
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(PlaintextAssertion)));

        Assert.Equal(expected, await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    // Current behavior with the flag off; PM-42283 changes this with the flag on.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CouldHandleAsync_PutWithForeignIssuerQuerySamlResponseAndSchemeQuery_ReturnsWhetherTheMessageIsInspected(
        bool rsa15DeprecationFlag, bool expected)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Method = HttpMethods.Put;
        testContext.Context.Request.QueryString = new QueryString(
            "?scheme=" + Scheme + "&SAMLResponse=" +
            Uri.EscapeDataString(EncodeDeflatedBase64(
                BuildResponseXml(PlaintextAssertion, "https://other-idp.example.com"))));

        Assert.Equal(expected, await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    // Current behavior with the flag off; PM-42283 changes this with the flag on.
    // The Sustainsys.Saml2 post binding compares the method with ordinal "POST".
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CouldHandleAsync_LowercasePostMethodWithFormSamlResponse_ReturnsWhetherThePostBindingReadsIt(
        bool rsa15DeprecationFlag, bool expected)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Method = "post";

        Assert.Equal(expected, await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_PostWithFormSamlResponseAndSamlRequest_InspectsSamlResponseFirst(
        bool rsa15DeprecationFlag)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = EncodeBase64(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1))),
            ["SAMLRequest"] = EncodeBase64(BuildResponseXml(PlaintextAssertion)),
        });

        if (rsa15DeprecationFlag)
        {
            // The gate rejects the rsa-1_5 message after the notice is queued.
            await Assert.ThrowsAsync<Exception>(() => options.CouldHandleAsync(Scheme, testContext.Context));
        }
        else
        {
            Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
        }

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_GetWithQuerySamlRequestAndSamlResponse_InspectsSamlRequestFirst(
        bool rsa15DeprecationFlag)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Method = HttpMethods.Get;
        testContext.Context.Request.QueryString = new QueryString(
            "?SAMLRequest=" + Uri.EscapeDataString(EncodeDeflatedBase64(BuildResponseXml(PlaintextAssertion))) +
            "&SAMLResponse=" + Uri.EscapeDataString(
                EncodeDeflatedBase64(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)))));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithRsa15FormAndBenignQueryMessages_InspectsFormMessageOnly()
    {
        // Current behavior with the flag off; PM-42283 changes this with the flag on.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)));
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(PlaintextAssertion)));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithRsa15FormAndBenignQueryMessagesAndRsa15DeprecationFlagOn_InspectsQueryMessageOnly()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)),
            rsa15DeprecationFlag: true);
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(PlaintextAssertion)));

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task CouldHandleAsync_SchemeQueryAndNoSamlMessageAndRsa15DeprecationFlagOn_ReturnsTrue(string method)
    {
        // Metadata and login-initiation requests carry no SAML message.
        var options = BuildOptions(wantAssertionsSigned: true);
        var context = BuildBareContext(method, rsa15DeprecationFlag: true);
        context.Request.QueryString = QueryString.Create("scheme", Scheme);

        Assert.True(await options.CouldHandleAsync(Scheme, context));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task CouldHandleAsync_NoSchemeQueryAndNoSamlMessageAndRsa15DeprecationFlagOn_ReturnsFalse(string method)
    {
        var options = BuildOptions(wantAssertionsSigned: true);
        var context = BuildBareContext(method, rsa15DeprecationFlag: true);

        Assert.False(await options.CouldHandleAsync(Scheme, context));
    }

    // Current behavior with the flag off; PM-42283 changes this with the flag on.
    // The middleware already rejects the artifact binding.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CouldHandleAsync_SchemeQueryAndArtifactMessage_ReturnsWhetherTheShortcutApplies(
        bool rsa15DeprecationFlag, bool expected)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var context = BuildBareContext(HttpMethods.Get, rsa15DeprecationFlag);
        context.Request.QueryString = new QueryString("?scheme=" + Scheme + "&SAMLart=AAQAAA");

        Assert.Equal(expected, await options.CouldHandleAsync(Scheme, context));
    }

    // With the flag off, the scheme shortcut applies.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task CouldHandleAsync_SchemeQueryAndRsa15FormMessage_QueuesNoticeOnlyWhenTheMessageIsInspected(
        bool rsa15DeprecationFlag, int expectedQueueCalls)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.QueryString = QueryString.Create("scheme", Scheme);

        if (rsa15DeprecationFlag)
        {
            // The gate rejects the rsa-1_5 message after the notice is queued.
            await Assert.ThrowsAsync<Exception>(() => options.CouldHandleAsync(Scheme, testContext.Context));
        }
        else
        {
            Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
        }

        testContext.Notifier.Received(expectedQueueCalls).TryQueue(OrganizationId);
    }

    // The legacy parser reads the first value. The Sustainsys.Saml2 request data requires a single value.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CouldHandleAsync_PostWithDuplicateFormSamlResponse_ReturnsWhetherTheRequestDataBuilds(
        bool rsa15DeprecationFlag, bool expected)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        var encoded = EncodeBase64(BuildResponseXml(PlaintextAssertion));
        testContext.Context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = new StringValues(new[] { encoded, encoded }),
        });

        Assert.Equal(expected, await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CouldHandleAsync_SchemeQueryAndLowercasePostMethodWithFormSamlResponse_ReturnsTrue(
        bool rsa15DeprecationFlag)
    {
        // With the flag on, no binding matches, so the shortcut applies and the handler finds no message.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion),
            rsa15DeprecationFlag: rsa15DeprecationFlag);
        testContext.Context.Request.Method = "post";
        testContext.Context.Request.QueryString = QueryString.Create("scheme", Scheme);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CouldHandleAsync_Rsa15Assertion_ReturnsTrueWithFlagOffAndThrowsWithFlagOn(
        bool rsa15DeprecationFlag, bool wantAssertionsSigned)
    {
        // A signed assertion decrypts inside the signature check. With the flag on, the gate rejects the
        // key transport algorithm before that check decrypts anything.
        var signingCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned, decryptionCertificate, signingCertificate);
        var encryptedAssertionXml = wantAssertionsSigned
            ? Saml2TestXml.EncryptAssertion(
                Saml2TestXml.BuildSignedAssertion(signingCertificate).OuterXml, decryptionCertificate, RsaPkcs1)
            : BuildEncryptedAssertion(RsaPkcs1);
        var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml),
            rsa15DeprecationFlag: rsa15DeprecationFlag);

        if (rsa15DeprecationFlag)
        {
            var exception = await Assert.ThrowsAsync<Exception>(
                () => options.CouldHandleAsync(Scheme, testContext.Context));
            Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
        }
        else
        {
            Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
        }
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15AssertionAndRsa15DeprecationFlagOn_QueuesNoticeThenThrows()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1)),
            rsa15DeprecationFlag: true);

        await Assert.ThrowsAsync<Exception>(() => options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.Received(1).TryQueue(OrganizationId);
    }

    [Fact]
    public async Task CouldHandleAsync_Rsa15AssertionWithForeignIssuerAndRsa15DeprecationFlagOn_ReturnsFalseWithoutNotice()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(
            BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1), "https://other-idp.example.com"),
            rsa15DeprecationFlag: true);

        Assert.False(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Theory]
    [InlineData(RsaOaepMgf1p)]
    [InlineData(RsaOaep)]
    public async Task CouldHandleAsync_AcceptedKeyTransportAndRsa15DeprecationFlagOn_ReturnsTrue(string algorithm)
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(BuildEncryptedAssertion(algorithm)),
            rsa15DeprecationFlag: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));

        testContext.Notifier.DidNotReceiveWithAnyArgs().TryQueue(default);
    }

    [Fact]
    public async Task CouldHandleAsync_SignedOaepAssertionAndRsa15DeprecationFlagOn_ReturnsTrue()
    {
        var signingCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test IdP");
        var decryptionCertificate = Saml2TestXml.CreateSelfSignedCertificate("CN=Test SP");
        var options = BuildOptions(wantAssertionsSigned: true, decryptionCertificate, signingCertificate);
        var encryptedAssertionXml = Saml2TestXml.EncryptAssertion(
            Saml2TestXml.BuildSignedAssertion(signingCertificate).OuterXml, decryptionCertificate);
        var testContext = BuildPostContext(BuildResponseXml(encryptedAssertionXml), rsa15DeprecationFlag: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_PlaintextAssertionAndRsa15DeprecationFlagOn_ReturnsTrue()
    {
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion), rsa15DeprecationFlag: true);

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithBenignFormAndRsa15QueryMessageAndRsa15DeprecationFlagOn_Throws()
    {
        // The redirect binding reads the query message, so the gate inspects it.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion), rsa15DeprecationFlag: true);
        testContext.Context.Request.QueryString = QueryString.Create(
            "SAMLResponse", EncodeDeflatedBase64(BuildResponseXml(BuildEncryptedAssertion(RsaPkcs1))));

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, testContext.Context));
        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_PostWithEmptyHostAndRsa15DeprecationFlagOn_ReturnsFalse()
    {
        // The Sustainsys.Saml2 request data cannot build a URL without a host, so the check fails closed.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion), rsa15DeprecationFlag: true);
        testContext.Context.Request.Host = new HostString(string.Empty);

        Assert.False(await options.CouldHandleAsync(Scheme, testContext.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_LogoutRequestWithRsa15EncryptedIdAndRsa15DeprecationFlagOn_ReturnsTrue()
    {
        // The gate covers only EncryptedAssertion elements. Sustainsys.Saml2 never decrypts an EncryptedID.
        var options = BuildOptions(wantAssertionsSigned: false);
        var testContext = BuildPostContext(BuildResponseXml(PlaintextAssertion), rsa15DeprecationFlag: true);
        var logoutRequest =
            "<samlp:LogoutRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
            "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
            "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
            "ID=\"_logoutrequest\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
            $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
            "<saml:EncryptedID><xenc:EncryptedKey>" +
            $"<xenc:EncryptionMethod Algorithm=\"{RsaPkcs1}\" />" +
            "</xenc:EncryptedKey></saml:EncryptedID>" +
            "</samlp:LogoutRequest>";
        testContext.Context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLRequest"] = EncodeBase64(logoutRequest),
        });

        Assert.True(await options.CouldHandleAsync(Scheme, testContext.Context));
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

    private static string BuildResponseXml(string assertionElement, string issuer = IdpEntityId) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{issuer}</saml:Issuer>" +
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

    private static DefaultHttpContext BuildRawPostContext(string formField, string value,
        bool rsa15DeprecationFlag = false)
    {
        var context = BuildBareContext(HttpMethods.Post, rsa15DeprecationFlag);
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [formField] = value,
        });
        return context;
    }

    private static DefaultHttpContext BuildGetContext(string queryField, string value,
        bool rsa15DeprecationFlag = false)
    {
        var context = BuildBareContext(HttpMethods.Get, rsa15DeprecationFlag);
        context.Request.QueryString = QueryString.Create(queryField, value);
        return context;
    }

    // The Sustainsys.Saml2 request data needs an absolute URL, so the request carries a scheme and host.
    private static DefaultHttpContext BuildBareContext(string method, bool rsa15DeprecationFlag = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("sso.example.com");
        context.Request.Path = ModulePath + "/Acs";
        context.RequestServices = BuildServices(featureFlagEnabled: true, rsa15DeprecationFlag, 7).Provider;
        return context;
    }

    private static string EncodeBase64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string EncodeDeflatedBase64(string value)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(value));
        }
        return Convert.ToBase64String(compressed.ToArray());
    }

    // CouldHandleAsync resolves the PM42982_WantAssertionsSigned feature flag (when WantAssertionsSigned is true)
    // from the request services.
    // It also resolves the global settings (for the RSA 1.5 email cooldown) and the RSA 1.5 notifier.
    private static PostTestContext BuildPostContext(string responseXml, bool featureFlagEnabled = true,
        int rsa15EmailCooldownInDays = 7, bool rsa15DeprecationFlag = false)
    {
        var context = BuildRawPostContext(responseXml);
        var services = BuildServices(featureFlagEnabled, rsa15DeprecationFlag, rsa15EmailCooldownInDays);
        context.RequestServices = services.Provider;
        return new PostTestContext(context, services.Notifier);
    }

    private static TestServices BuildServices(bool featureFlagEnabled, bool rsa15DeprecationFlag,
        int rsa15EmailCooldownInDays)
    {
        var services = new ServiceCollection();

        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.PM42982_WantAssertionsSigned).Returns(featureFlagEnabled);
        featureService.IsEnabled(FeatureFlagKeys.PM42283_Rsa15Deprecation).Returns(rsa15DeprecationFlag);
        services.AddSingleton(featureService);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Sso.Rsa15DeprecationEmailCooldownInDays.Returns(rsa15EmailCooldownInDays);
        services.AddSingleton(globalSettings);

        var notifier = Substitute.For<ISaml2Rsa15DeprecationNotifier>();
        services.AddSingleton(notifier);

        return new TestServices(services.BuildServiceProvider(), notifier);
    }

    private sealed record TestServices(IServiceProvider Provider, ISaml2Rsa15DeprecationNotifier Notifier);

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
