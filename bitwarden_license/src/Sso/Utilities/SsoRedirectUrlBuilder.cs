namespace Bit.Sso.Utilities;

/// <summary>
/// Builds redirect URLs back to the web client's /login from the SSO callback
/// when authentication is refused for a recoverable reason. Localizes the URL
/// contract in one place so future scenarios reuse the same shape rather than
/// duplicating query-string composition at each catch site.
/// </summary>
public static class SsoRedirectUrlBuilder
{
    /// <summary>
    /// Stable error codes appended to the redirect URL as the `error` query
    /// param. The web client's WebLoginComponentService switches on these.
    /// Adding a new scenario: add a constant here, update the switch in
    /// web-login-component.service.ts, add a matching i18n key.
    /// </summary>
    public static class ErrorCodes
    {
        public const string InviteAcceptanceRequired = "ssoOrgInviteAcceptanceRequired";
        // Future: AccessRevoked = "ssoOrganizationAccessRevoked", etc.
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
}
