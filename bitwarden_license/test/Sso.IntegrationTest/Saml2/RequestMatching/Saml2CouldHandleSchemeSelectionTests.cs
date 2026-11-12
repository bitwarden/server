using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Utilities.Saml2;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Bit.Sso.IntegrationTest.Utilities.Saml2CouldHandleTestHelpers;
using static Bit.Sso.IntegrationTest.Utilities.Saml2MessageBuilder;

namespace Bit.Sso.IntegrationTest.Saml2.RequestMatching;

/// <summary>
/// Exercises how <c>CouldHandleAsync</c> decides if the scheme handles a request: the <c>scheme</c> query
/// string shortcut, requests that carry no SAML message, a message from another identity provider, and a
/// message that cannot be read. A declined request returns false without throwing, so that the middleware
/// can try the next scheme.
/// </summary>
public class Saml2CouldHandleSchemeSelectionTests
{
    [Fact]
    public async Task CouldHandleAsync_SchemeQueryAndUnsignedEnvelopeAndWantAssertionsSigned_ReturnsTrue()
    {
        // With the flag off, the scheme shortcut returns true. Current behavior; PM-42283 changes this
        // with the flag on.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var arrangement = await ArrangeAsync(
            BuildAssertionDocument().DocumentElement!.OuterXml, idpCertificate, spCertificate,
            wantAssertionsSigned: true);
        arrangement.Context.Request.QueryString = QueryString.Create("scheme", arrangement.Scheme);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_SchemeQueryAndUnsignedEnvelopeAndWantAssertionsSignedAndRsa15DeprecationFlagOn_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var arrangement = await ArrangeAsync(
            BuildAssertionDocument().DocumentElement!.OuterXml, idpCertificate, spCertificate,
            wantAssertionsSigned: true, rsa15DeprecationFlag: true);
        arrangement.Context.Request.QueryString = QueryString.Create("scheme", arrangement.Scheme);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    // Current behavior with the flag off; PM-42283 changes this with the flag on.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CouldHandleAsync_SchemeQueryAndForeignIssuer_ReturnsWhetherTheShortcutApplies(
        bool rsa15DeprecationFlag, bool expected)
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned: true, rsa15DeprecationFlag: rsa15DeprecationFlag);
        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId), "SAMLResponse",
            BuildResponseXml(BuildAssertionDocument().DocumentElement!.OuterXml, "https://other-idp.example.com"));
        context.Request.QueryString = QueryString.Create("scheme", organizationId);

        Assert.Equal(expected, await samlOptions.CouldHandleAsync(organizationId, context));
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("POST", false)]
    [InlineData("GET", true)]
    [InlineData("POST", true)]
    public async Task CouldHandleAsync_SchemeQueryAndNoSamlMessage_ReturnsTrue(
        string method, bool rsa15DeprecationFlag)
    {
        // Metadata and login-initiation requests carry no SAML message.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned: true, rsa15DeprecationFlag: rsa15DeprecationFlag);
        var context = BuildNoMessageContext(testData, organizationId, method);
        context.Request.QueryString = QueryString.Create("scheme", organizationId);

        Assert.True(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("POST", false)]
    [InlineData("GET", true)]
    [InlineData("POST", true)]
    public async Task CouldHandleAsync_NoSchemeQueryAndNoSamlMessage_ReturnsFalse(
        string method, bool rsa15DeprecationFlag)
    {
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned: true, rsa15DeprecationFlag: rsa15DeprecationFlag);
        var context = BuildNoMessageContext(testData, organizationId, method);

        Assert.False(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    [Fact]
    public async Task CouldHandleAsync_IssuerDoesNotMatchIdp_ReturnsFalseWithoutThrowing()
    {
        // Scheme-selection invariant. A message from another identity provider must decline the
        // scheme, not throw, even with an unsigned assertion and the setting on. Any change to
        // the signature check must keep this ordering.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned: true);

        var foreignResponse = BuildResponseXml(
            BuildAssertionDocument().DocumentElement!.OuterXml, "https://other-idp.example.com");
        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId), "SAMLResponse", foreignResponse);

        Assert.False(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    [Fact]
    public async Task CouldHandleAsync_UnparseablePayload_ReturnsFalseWithoutThrowing()
    {
        // Scheme-selection invariant. Malformed input must decline the scheme quietly so the
        // middleware can try the next one.
        var (idpCertificate, spCertificate) = BuildCertificates();
        var (testData, samlOptions, organizationId) =
            await BuildSchemeAsync(idpCertificate, spCertificate, wantAssertionsSigned: true);

        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Path = SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = "not-base64-at-all!!",
        });

        Assert.False(await samlOptions.CouldHandleAsync(organizationId, context));
    }

    private static HttpContext BuildNoMessageContext(SsoTestData testData, string organizationId, string method)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = testData.Factory.Services.CreateScope().ServiceProvider,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("sso.example.com");
        context.Request.Path = SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId);
        context.Request.Method = method;
        if (method == HttpMethods.Post)
        {
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
            {
                ["RelayState"] = "unrelated",
            });
        }
        return context;
    }
}
