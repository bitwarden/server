using Bit.Sso.IntegrationTest.Utilities;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Sends real HTTP <c>POST</c> requests to the assertion consumer service (ACS) URL.
/// The requests pass through the whole pipeline. The other SAML tests in this project call
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
}
