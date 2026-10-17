namespace Bit.Admin.Auth.IdentityServer;

public static class AdminAuthenticationSchemes
{
    // Registered scheme name for the upstream federated OIDC handler. Used by controller
    // actions to Challenge/SignOut the correct handler when the app has multiple auth
    // schemes registered (this one plus ASP.NET Identity's application/external cookies).
    public const string UpstreamOidc = "sso";

    // Application cookie issued at the end of a successful SSO sign-in. Separate from the
    // Identity application cookie by design: the SSO path has no local user store and no
    // SecurityStamp to track - the IdP owns identity, so routing it through the Identity
    // cookie (whose SecurityStampValidator does a 30s re-fetch against the user store) would
    // sign the session out within 30 seconds. Startup's authorization DefaultPolicy lists
    // both this scheme and IdentityConstants.ApplicationScheme so [Authorize] accepts either.
    public const string SsoCookie = "sso-cookie";
}
