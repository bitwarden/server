namespace Bit.Admin.Auth.IdentityServer;

public static class AdminAuthenticationSchemes
{
    // Registered scheme name for the upstream federated OIDC handler. Used by controller
    // actions to Challenge/SignOut the correct handler when the app has multiple auth
    // schemes registered (this one plus ASP.NET Identity's application/external cookies).
    public const string UpstreamOidc = "sso";
}
