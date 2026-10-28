using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that a user is eligible to retrieve the invite for an invite link.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item><see cref="IInviteLinkEligibilityValidationService.ValidateEligibility"/>.</item>
///     <item>The existing membership's status:
///     <see cref="InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus"/> when the link supports confirmation,
///     otherwise <see cref="InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus"/>.</item>
/// </list>
/// The caller supplies the invite link, organization, and existing membership. The reset password key is left to the
/// accept and confirm endpoints.
/// </remarks>
public interface IGetOrganizationInviteValidator
{
    ValidationResult<OrganizationInviteLinkValidationRequest> Validate(OrganizationInviteLinkValidationRequest request);
}
