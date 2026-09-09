namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because the existing Bitwarden user
/// has an outstanding organization invite that must be accepted (via master password
/// login) before SSO can proceed.
///
/// Carries the organization id + display name and the invited org user's email so
/// that the SSO callback can redirect the user back to the web client's /login
/// with enough context for the client to match the redirect against its stashed
/// invite and surface an actionable toast. The id is the stable match key — the
/// display name can drift between when the invite was sent and when SSO is
/// attempted.
/// </summary>
public class SsoAuthnRequiresInviteAcceptanceException : Exception
{
    public Guid OrganizationId { get; }
    public string OrganizationDisplayName { get; }
    public string UserEmail { get; }

    public SsoAuthnRequiresInviteAcceptanceException(
        Guid organizationId, string organizationDisplayName, string userEmail)
        : base($"Invite acceptance required before SSO for org '{organizationDisplayName}'.")
    {
        OrganizationId = organizationId;
        OrganizationDisplayName = organizationDisplayName;
        UserEmail = userEmail;
    }
}
