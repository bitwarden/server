using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

/// <summary>
/// Validates that a user is eligible to retrieve the invite for an invite link.
/// </summary>
/// <remarks>
/// The following are validated, in this order:
/// <list type="bullet">
///     <item><see cref="InviteLinkRules.ValidateEligibility"/>: the invite link exists and its code
///     matches, its organization is enabled and supports invite links, the user's email domain is allowed, and the
///     email is verified.</item>
///     <item>The existing membership's status:
///     <see cref="InviteLinkRules.ValidateConfirmMembershipStatus"/> when the link supports confirmation,
///     otherwise <see cref="InviteLinkRules.ValidateAcceptMembershipStatus"/>.</item>
///     <item><see cref="IInviteLinkValidationService.ValidateSeatAvailabilityAsync"/>.</item>
///     <item><see cref="IInviteLinkValidationService.ValidateJoinRequirementsAsync"/>.</item>
/// </list>
/// The caller supplies the invite link, organization, and existing membership. The reset password key is left to the
/// accept and confirm endpoints.
/// </remarks>
public interface IGetOrganizationInviteValidator
{
    Task<ValidationResult<OrganizationInviteLinkValidationRequest>> ValidateAsync(
        OrganizationInviteLinkValidationRequest request);
}
