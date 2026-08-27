namespace Bit.Sso.Utilities;

/// <summary>
/// Builds redirect URLs the SSO callback sends the browser to when authentication
/// is refused. Two shapes are supported: a <c>/login</c> redirect for cases the
/// user can retry after a stashed-invite match, and a <c>/sso-login-failed</c>
/// direct redirect for cases with no user-side remediation. Localized here so
/// query-string composition doesn't drift across catch sites.
/// </summary>
public static class SsoRedirectUrlBuilder
{
    /// <summary>
    /// Stable error codes appended to the /login redirect URL as the `error` query
    /// param. The web client's WebLoginComponentService switches on these.
    /// Adding a new scenario: add a constant here, update the switch in
    /// web-login-component.service.ts, add a matching i18n key.
    /// </summary>
    public static class ErrorCodes
    {
        public const string InviteAcceptanceRequired = "ssoOrgInviteAcceptanceRequired";
        public const string OrgMembershipRequired = "ssoOrgMembershipRequired";
        public const string StagedOrgUserInviteAcceptanceRequired = "ssoStagedOrgUserInviteAcceptanceRequired";
        // Future: AccessRevoked = "ssoOrganizationAccessRevoked", etc.
    }

    /// <summary>
    /// Stable kind identifiers appended to the /sso-login-failed redirect URL as
    /// the `kind` query param. Must match the web client's
    /// <c>SsoLoginFailedErrorKind</c> string values.
    /// </summary>
    public static class SsoLoginFailedErrorKind
    {
        public const string NoSeatsAvailable = "no-seats-available";
    }

    /// <summary>
    /// Composes a redirect URL of the form
    /// <c>{vaultWithHashUrl}/login?email=…&amp;organizationId=…&amp;organizationName=…&amp;error=…</c>.
    /// Email and organization name are URL-encoded; the organization id is rendered
    /// as a bare GUID string; the error code is treated as a server-controlled
    /// constant and is not encoded.
    /// </summary>
    /// <param name="vaultWithHashUrl">The web vault base URL including the hash fragment marker
    /// (e.g. <c>https://vault.bitwarden.com/#</c>), as exposed by
    /// <c>IGlobalSettings.BaseServiceUri.VaultWithHash</c>.</param>
    /// <param name="email">The invited org user's email, pre-filled into the login form.</param>
    /// <param name="organizationId">The organization id. The client uses this as the stable
    /// match key against its locally stashed invite — display names can drift between
    /// when an invite is sent and when SSO is attempted, so id is the source of truth.</param>
    /// <param name="organizationDisplayName">The organization display name, surfaced in the toast.</param>
    /// <param name="errorCode">A constant from <see cref="ErrorCodes"/>.</param>
    public static string BuildLoginRedirectUrl(
        string vaultWithHashUrl,
        string email,
        Guid organizationId,
        string organizationDisplayName,
        string errorCode)
    {
        var qs = $"email={Uri.EscapeDataString(email)}"
               + $"&organizationId={organizationId}"
               + $"&organizationName={Uri.EscapeDataString(organizationDisplayName)}"
               + $"&error={errorCode}";
        return $"{vaultWithHashUrl}/login?{qs}";
    }

    /// <summary>
    /// Composes a redirect URL of the form
    /// <c>{vaultWithHashUrl}/sso-login-failed?kind=…</c>. The <c>kind</c> value is
    /// a server-controlled constant from <see cref="SsoLoginFailedErrorKind"/>.
    /// </summary>
    public static string BuildSsoLoginFailedRedirectUrl(string vaultWithHashUrl, string kind)
    {
        return $"{vaultWithHashUrl}/sso-login-failed?kind={kind}";
    }
}
