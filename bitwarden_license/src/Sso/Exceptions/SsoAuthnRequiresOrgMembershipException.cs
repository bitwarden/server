namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because the existing Bitwarden user
/// has no <see cref="Bit.Core.Entities.OrganizationUser"/> row in the target org.
/// Membership must be established via invite acceptance (direct or open, via master
/// password) before SSO can proceed.
///
/// Two scenarios converge here and the server cannot tell them apart:
///   1. Existing user clicked an open invite link (client has it stashed) then picked SSO.
///   2. Existing user has no pending invite — never invited to this org.
///
/// The client redirect handler disambiguates by matching organizationId against
/// any stashed invite: match → fast-forward to MP entry (the user clicked an open
/// invite link); no match → reuse the existing
/// <c>ssoLoginRequiresInviteAcceptance</c> toast on /login (the user has no pending
/// invite at all).
/// </summary>
public class SsoAuthnRequiresOrgMembershipException : Exception
{
    public Guid OrganizationId { get; }
    public string OrganizationDisplayName { get; }
    public string UserEmail { get; }

    public SsoAuthnRequiresOrgMembershipException(
        Guid organizationId, string organizationDisplayName, string userEmail)
        : base($"Org membership required before SSO for org '{organizationDisplayName}'.")
    {
        OrganizationId = organizationId;
        OrganizationDisplayName = organizationDisplayName;
        UserEmail = userEmail;
    }
}
