using System.Net;
using Xunit;

namespace Bit.Sso.IntegrationTest.Helpers;

/// <summary>
/// Assertions on the responses that <c>/Account/ExternalCallback</c> returns.
/// </summary>
public static class ExternalCallbackAssertions
{
    /// <summary>
    /// Asserts the response is a 302 redirect to the web client's <c>/sso-login-failed</c> terminal page
    /// with the <c>no-seats-available</c> kind.
    /// </summary>
    public static void AssertNoSeatsAvailableRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location.ToString();
        Assert.Contains("/sso-login-failed?", location);
        Assert.Contains("kind=no-seats-available", location);
    }

    /// <summary>
    /// Asserts the response is a 302 redirect to the web client's <c>/login</c> page with the
    /// <c>ssoStagedOrgUserInviteAcceptanceRequired</c> error.
    /// </summary>
    public static void AssertStagedInviteAcceptanceRequiredRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoStagedOrgUserInviteAcceptanceRequired", location);
    }
}
