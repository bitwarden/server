using Bit.Sso.Utilities.Saml2;
using Xunit;
using static Bit.Sso.IntegrationTest.Helpers.Saml2CouldHandleTestHelpers;
using static Bit.Sso.IntegrationTest.Helpers.Saml2MessageBuilder;

namespace Bit.Sso.IntegrationTest.Saml2.RequestMatching;

/// <summary>
/// A response with no assertions passes the assertion signature check in <c>CouldHandleAsync</c> only when its
/// <c>samlp:Status</c> has a readable error code. White space around the code is allowed. A success status or a
/// status with no readable code throws. No status excuses an unsigned assertion, and neither does a missing
/// status.
/// </summary>
public class Saml2CouldHandleStatusCodeTests
{
    [Fact]
    public async Task CouldHandleAsync_ErrorStatusResponseWithNoAssertions_DoesNotThrow()
    {
        // An identity provider that refuses to authenticate returns a <Response> with an error
        // status and no assertions, per SAML Profiles 4.1.4.1. The message is well-formed and has
        // no assertion to check, so this check must let it through and let the handler pipeline
        // report the real status.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus),
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    [Fact]
    public async Task CouldHandleAsync_ErrorStatusResponseWithUnsignedAssertion_Throws()
    {
        // Guards against a too-broad status gate. An error status must not excuse an assertion
        // that is actually present. A response in this shape breaks SAML Profiles 4.1.4.1, so
        // its assertions get the same scrutiny as any other.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.ResponderStatus, Saml2ResponseTypes.AuthnFailedStatus) +
            BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_MissingStatusElementWithUnsignedAssertion_Throws()
    {
        // Fail-closed guard. <samlp:Status> is mandatory in a <Response>, but a caller controls
        // the payload and can omit it. An absent element must never be read as "not a success".
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SuccessStatusResponseWithUnsignedAssertion_Throws()
    {
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus) + BuildAssertionDocument().DocumentElement!.OuterXml,
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_SuccessStatusResponseWithNoAssertions_Throws()
    {
        // A successful authentication with no assertion is unexpected. 
        // The status gate must not turn this into a pass.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            BuildStatusXml(Saml2ResponseTypes.SuccessStatus), idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Theory]
    [InlineData("<samlp:Status />")]
    [InlineData("<samlp:Status><samlp:StatusCode Value=\"\" /></samlp:Status>")]
    [InlineData("<samlp:Status><samlp:StatusCode Value=\"   \" /></samlp:Status>")]
    public async Task CouldHandleAsync_StatusWithoutReadableCodeAndNoAssertions_Throws(string statusXml)
    {
        // Fail-closed. Only a status code that reads as a real error excuses a response carrying
        // no assertions. A <Status> element with nothing usable inside it must not.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            statusXml, idpCertificate, spCertificate, wantAssertionsSigned: true);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    [Fact]
    public async Task CouldHandleAsync_WhitespacePaddedErrorStatusWithNoAssertions_DoesNotThrow()
    {
        // XML keeps leading and trailing spaces in an attribute value. The status read trims
        // before comparing, so padding does not turn a refusal into a signature failure.
        var (idpCertificate, spCertificate) = BuildCertificates();

        var arrangement = await ArrangeAsync(
            $"<samlp:Status><samlp:StatusCode Value=\"  {Saml2ResponseTypes.ResponderStatus}  \" /></samlp:Status>",
            idpCertificate, spCertificate, wantAssertionsSigned: true);

        Assert.True(await arrangement.SamlOptions.CouldHandleAsync(arrangement.Scheme, arrangement.Context));
    }

    // SAML Core 3.2.2.2 nests the second-level status code inside the top-level one, so this
    // also exercises reading the outer Value rather than the inner one.
    private static string BuildStatusXml(string topLevelCode, string? secondLevelCode = null) =>
        $"<samlp:Status><samlp:StatusCode Value=\"{topLevelCode}\">" +
        (secondLevelCode == null ? string.Empty : $"<samlp:StatusCode Value=\"{secondLevelCode}\" />") +
        "</samlp:StatusCode></samlp:Status>";
}
