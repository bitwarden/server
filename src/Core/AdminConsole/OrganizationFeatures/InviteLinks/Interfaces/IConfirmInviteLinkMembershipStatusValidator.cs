using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that the user's existing membership allows confirming through an invite link: it must not be revoked,
/// and the user must not already be confirmed. An accepted membership is allowed, so it can be confirmed.
/// </summary>
public interface IConfirmInviteLinkMembershipStatusValidator
{
    ValidationResult<InviteLinkMembershipStatusValidationRequest> Validate(InviteLinkMembershipStatusValidationRequest request);
}
