// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin;

public class AdminSettings
{
    public virtual string Admins { get; set; }
    public int? DeleteTrashDaysAgo { get; set; }
    public virtual OidcSettings Oidc { get; set; } = new OidcSettings();

    public bool OidcEnabled =>
        Oidc != null &&
        !string.IsNullOrWhiteSpace(Oidc.Authority) &&
        !string.IsNullOrWhiteSpace(Oidc.ClientId) &&
        !string.IsNullOrWhiteSpace(Oidc.ClientSecret);

    public class OidcSettings
    {
        public string Authority { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string CallbackPath { get; set; } = "/login/sso-callback";
        public string SignedOutCallbackPath { get; set; } = "/login/sso-signout";
        public string Scopes { get; set; } = "openid profile email";
        public string EmailClaimType { get; set; } = "email";
        public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;
        public string DisplayName { get; set; } = "SSO";
    }
}
