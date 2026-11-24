using System.Net;
using System.Text.Json;
using Bit.Sso.IntegrationTest.Helpers;
using Bit.Sso.Utilities.Saml2;
using Xunit;
using EncryptedXml = System.Security.Cryptography.Xml.EncryptedXml;

namespace Bit.Sso.IntegrationTest.Saml2.Acs;

/// <summary>
/// Sends real HTTP <c>POST</c> requests to the assertion consumer service (ACS) URL.
/// The requests pass through the whole pipeline. Most other SAML tests in this project call
/// <c>CouldHandleAsync</c> directly and never reach the authentication handler.
/// </summary>
public class Saml2AcsRequestPathTests
{
    [Fact]
    public async Task AcsPost_Rsa15AndRsaOaepMgf1pAssertions_ReturnTheSameResult()
    {
        // The relative check proves that the pipeline treats an rsa-1_5 key transport the same as an
        // accepted rsa-oaep-mgf1p key transport. It asserts no absolute status code.
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            Saml2AcsPostHarness.BuildEncryptedAssertion(Saml2AcsPostHarness.Rsa15));

        using var rsa15Response = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, arrangement.ResponseXml);
        using var oaepResponse = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId,
            Saml2AcsPostHarness.BuildResponseXml(
                Saml2AcsPostHarness.BuildEncryptedAssertion(Saml2AcsPostHarness.RsaOaepMgf1p)));

        Assert.Equal(oaepResponse.StatusCode, rsa15Response.StatusCode);
        Assert.Null(rsa15Response.Headers.Location);
        Assert.Null(oaepResponse.Headers.Location);
        Assert.False(rsa15Response.Headers.Contains("Set-Cookie"));
        Assert.False(oaepResponse.Headers.Contains("Set-Cookie"));
    }

    /// <summary>
    /// Checks that a real HTTP request to the ACS URL runs the key transport check before the
    /// Sustainsys.Saml2 handler can decrypt the assertion. The PM42283_Rsa15Deprecation flag is on.
    /// The test sends two requests, which differ only in the key transport algorithm:
    /// <list type="number">
    /// <item>An rsa-1_5 assertion must fail in <c>CouldHandleAsync</c>, and the handler must not run.</item>
    /// <item>An rsa-oaep-mgf1p assertion must pass the check and reach the handler. The handler then
    /// rejects it because it is unsigned. The test checks only that the request reached the handler.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AcsPost_Rsa15AssertionAndRsa15DeprecationFlagOn_FailsBeforeTheHandlerRuns()
    {
        // Arrange
        var spCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, rsa15DeprecationFlag: true, spCertificate: spCertificate,
            allowUnsolicitedResponse: true);
        var assertion = Saml2AcsPostHarness.BuildSubjectAssertion("user@test.com");
        string BuildResponseXml(string keyTransportAlgorithm) => Saml2AcsPostHarness.BuildResponseXml(
            Saml2AcsPostHarness.EncryptAssertion(assertion, spCertificate, keyTransportAlgorithm),
            includeSuccessStatus: true);

        // Act
        using var rsa15Response = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, BuildResponseXml(EncryptedXml.XmlEncRSA15Url));
        using var oaepResponse = await Saml2AcsPostHarness.PostAcsAsync(
            arrangement.Client, arrangement.OrganizationId, BuildResponseXml(EncryptedXml.XmlEncRSAOAEPUrl));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, rsa15Response.StatusCode);
        Assert.Null(rsa15Response.Headers.Location);
        Assert.False(rsa15Response.Headers.Contains("Set-Cookie"));
        var (rsa15Detail, rsa15Stack) = await ReadProblemAsync(rsa15Response);
        Assert.Equal(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, rsa15Detail);
        Assert.Contains("Saml2OptionsExtensions.CouldHandleWithOaepGateAsync", rsa15Stack);
        Assert.DoesNotContain("Saml2Handler", rsa15Stack);

        var (oaepDetail, oaepStack) = await ReadProblemAsync(oaepResponse);
        Assert.NotEqual(Saml2EncryptedAssertionInspector.KeyTransportAlgorithmNotAcceptedMessage, oaepDetail);
        Assert.Contains("Saml2Handler", oaepStack);
    }

    // The test host returns an unhandled exception as a problem details body.
    private static async Task<(string? Detail, string Stack)> ReadProblemAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        return (root.GetProperty("detail").GetString(),
            root.GetProperty("exception").GetProperty("details").GetString() ?? string.Empty);
    }
}
