using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that the user's existing membership allows accepting an invite link: it must not be revoked, and the
/// user must not already be an accepted or confirmed member.
/// </summary>
public interface IAcceptInviteLinkMembershipStatusValidator
{
    ValidationResult<InviteLinkMembershipStatusValidationRequest> Validate(InviteLinkMembershipStatusValidationRequest request);
}
