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
    /// <c>{vaultWithHashUrl}/login?email=…&amp;organizationName=…&amp;error=…</c>.
    /// Email and organization name are URL-encoded; the error code is treated as
    /// a server-controlled constant and is not encoded.
    /// </summary>
    /// <param name="vaultWithHashUrl">The web vault base URL including the hash fragment marker
    /// (e.g. <c>https://vault.bitwarden.com/#</c>), as exposed by
    /// <c>IGlobalSettings.BaseServiceUri.VaultWithHash</c>.</param>
    /// <param name="email">The invited org user's email, pre-filled into the login form.</param>
    /// <param name="organizationDisplayName">The organization display name, surfaced in the toast.</param>
    /// <param name="errorCode">A constant from <see cref="ErrorCodes"/>.</param>
    /// <param name="autoSubmit">When true, the client auto-progresses past the email-entry step
    /// to the master-password entry step (saves a click on a flow where the redirect already
    /// supplied the email). Emitted as <c>&amp;autoSubmit=true</c> only when true.</param>
    public static string BuildLoginRedirectUrl(
        string vaultWithHashUrl,
        string email,
        string organizationDisplayName,
        string errorCode,
        bool autoSubmit = false)
    {
        var qs = $"email={Uri.EscapeDataString(email)}"
               + $"&organizationName={Uri.EscapeDataString(organizationDisplayName)}"
               + $"&error={errorCode}";
        if (autoSubmit)
        {
            qs += "&autoSubmit=true";
        }
        return $"{vaultWithHashUrl}/login?{qs}";
    }
}
