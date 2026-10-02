namespace Bit.Sso.Exceptions;

/// <summary>
/// Thrown when SSO authentication is refused because the existing Bitwarden user
/// matches an <see cref="Bit.Core.Enums.OrganizationUserStatusType.Staged"/>
/// OrganizationUser row that was just promoted to
/// <see cref="Bit.Core.Enums.OrganizationUserStatusType.Invited"/> as part of this
/// SSO attempt. A fresh invite email has been sent; the user must accept it (via
/// master password login) before SSO can proceed.
///
/// Distinct from <see cref="SsoAuthnRequiresInviteAcceptanceException"/> so the
/// client can tell the user to check their email for a newly-sent invite rather
/// than referencing an invite they should already have received.
/// </summary>
public class SsoAuthnStagedOrgUserRequiresInviteAcceptanceException : Exception
{
    public Guid OrganizationId { get; }
    public string OrganizationDisplayName { get; }
    public string UserEmail { get; }

    public SsoAuthnStagedOrgUserRequiresInviteAcceptanceException(
        Guid organizationId, string organizationDisplayName, string userEmail)
        : base($"Staged OrganizationUser promoted to Invited and direct org invite email sent; invite acceptance required before SSO for org '{organizationDisplayName}'.")
    {
        OrganizationId = organizationId;
        OrganizationDisplayName = organizationDisplayName;
        UserEmail = userEmail;
    }
}
