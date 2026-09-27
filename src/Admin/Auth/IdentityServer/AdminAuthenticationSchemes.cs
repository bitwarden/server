namespace Bit.Admin.Auth.IdentityServer;

public static class AdminAuthenticationSchemes
{
    public const string UpstreamOidc = "sso";
    public const string AuthMethodClaimType = "admin_auth_method";
    public const string AuthMethodSso = "sso";

    // Sent to the IdP as `max_age` on the authorize request and enforced server-side by
    // SsoSignIn against the returned `auth_time` claim. Shared constant so the hint we send
    // and the enforcement we apply can't drift apart.
    public const int MaxIdpAuthAgeSeconds = 3600;
}
