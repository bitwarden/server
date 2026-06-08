namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because the existing Bitwarden user
/// has an outstanding organization invite that must be accepted (via master password
/// login) before SSO can proceed.
///
/// Carries the organization display name and the invited org user's email so that
/// the SSO callback can redirect the user back to the web client's /login with
/// enough context for the client to surface an actionable toast.
/// </summary>
public class SsoAuthnRequiresInviteAcceptanceException : Exception
{
    public string OrganizationDisplayName { get; }
    public string UserEmail { get; }

    public SsoAuthnRequiresInviteAcceptanceException(
        string organizationDisplayName, string userEmail)
        : base($"Invite acceptance required before SSO for org '{organizationDisplayName}'.")
    {
        OrganizationDisplayName = organizationDisplayName;
        UserEmail = userEmail;
    }
}
