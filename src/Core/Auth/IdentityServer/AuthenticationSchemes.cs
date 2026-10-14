namespace Bit.Core.Auth.IdentityServer;

public static class AuthenticationSchemes
{
    /// <summary>
    /// Cookie scheme that holds the Identity app's external login ticket.
    /// </summary>
    public const string IdentityExternalCookie = "bw.identity.external";

    /// <summary>
    /// Cookie scheme that holds the Sso app's external login ticket.
    /// </summary>
    public const string SsoExternalCookie = "bw.sso.external";

    /// <summary>
    /// OpenID Connect scheme that the Identity app challenges for SSO logins.
    /// </summary>
    public const string IdentitySsoOidc = "sso";
}
