// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin;

public class AdminSettings
{
    // Comma-separated allowlist of email addresses permitted to sign in to the Admin Portal.
    // Authorization gate for both passwordless and SSO paths - authentication proves identity,
    // this list decides who's an admin.
    public virtual string Admins { get; set; }

    // Retention window for soft-deleted items before the background cleanup job hard-deletes them.
    // Null disables the job.
    public int? DeleteTrashDaysAgo { get; set; }

    // Kill switch for the email magic-link login flow. Set to false to require SSO exclusively -
    // useful in environments where the operator wants all admin access to flow through an audited IdP.
    public bool EnablePasswordlessLogin { get; set; } = true;

    // Upstream OIDC (federated SSO) configuration. Presence of Authority/ClientId/ClientSecret
    // enables the SSO flow; absent, only the passwordless email flow is available.
    public OidcSettings Oidc { get; set; } = new OidcSettings();

    // SSO is enabled only when the three values with no defaults are configured. Fail-closed so
    // an incomplete config surfaces as "SSO off" rather than a broken flow.
    public bool OidcEnabled =>
        Oidc != null &&
        !string.IsNullOrWhiteSpace(Oidc.Authority) &&
        !string.IsNullOrWhiteSpace(Oidc.ClientId) &&
        !string.IsNullOrWhiteSpace(Oidc.ClientSecret);

    public class OidcSettings
    {
        // Tolerance applied to JWT `exp`/`nbf`/`iat` validation via TokenValidationParameters
        // .ClockSkew. Absorbs ordinary NTP drift between the IdP and this app so a slightly
        // fast/slow IdP clock doesn't reject every SSO login.
        public const int ClockSkewSeconds = 120;


        // OIDC discovery root. The handler appends /.well-known/openid-configuration to fetch endpoints and JWKS.
        public string Authority { get; set; }

        // Registered application ID at the IdP. Sent as `client_id` on the authorize request
        // and used to identify the client on the token exchange.
        public string ClientId { get; set; }

        // Registered application secret at the IdP. Combined with ClientId on the token exchange
        // (client_secret_post). Rotate on IdP-side rotation.
        public string ClientSecret { get; set; }

        // Where the IdP posts the authorization code back to. Must exactly match the "Allowed
        // Callback URLs" registered at the IdP, including the app's PathBase (e.g., /admin for
        // self-hosted).
        public string CallbackPath { get; set; } = "/login/sso-callback";

        // Where the IdP redirects after RP-initiated logout completes. Must exactly match the
        // IdP's "Allowed Logout URLs" list. Sent to the IdP as post_logout_redirect_uri.
        public string SignedOutCallbackPath { get; set; } = "/login/sso-signout";

        // Space-delimited scopes requested during authorize. Must include `openid` (this is an
        // OIDC flow) plus whatever is needed to obtain the email claim used for authorization.
        public string Scopes { get; set; } = "openid profile email";

        // Which claim on the principal holds the email address. OIDC standard is `email`; some
        // IdPs use custom claim types (e.g., `preferred_username` when it holds the email).
        public string EmailClaimType { get; set; } = "email";

        // Label shown on the "Sign in with X" button and in error messages. Defaults to "SSO" -
        // set to the IdP's product name for a friendlier UX.
        public string DisplayName { get; set; } = "SSO";

        // When true, reject sign-in unless the principal presents `email_verified=true`. Protects
        // against IdPs that permit self-registration with unverified email addresses. Set to
        // false only for IdPs that don't emit the claim and are operator-controlled.
        public bool RequireEmailVerifiedClaim { get; set; } = true;
    }
}
