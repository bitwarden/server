using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IGetOrganizationInviteValidator"/>.
/// </summary>
public class GetOrganizationInviteValidator(IInviteLinkEligibilityValidationService inviteLinkEligibilityValidationService)
    : IGetOrganizationInviteValidator
{
    public ValidationResult<OrganizationInviteLinkValidationRequest> Validate(
        OrganizationInviteLinkValidationRequest request)
    {
        var eligibilityError = inviteLinkEligibilityValidationService.ValidateEligibility(
            request.ExistingInviteLink,
            request.Code,
            request.Organization,
            request.User);
        if (eligibilityError is not null)
        {
            return Invalid(request, eligibilityError);
        }

        var membershipStatusError = ValidateMembershipStatus(request);
        if (membershipStatusError is not null)
        {
            return Invalid(request, membershipStatusError);
        }

        return Valid(request);
    }

    /// <summary>
    /// The existing membership must allow the step the link leads to: confirming, or accepting.
    /// </summary>
    /// <remarks>
    /// Must run after the eligibility check, which guarantees the invite link and organization exist.
    /// </remarks>
    private static Error? ValidateMembershipStatus(OrganizationInviteLinkValidationRequest request) =>
        request.ExistingInviteLink!.SupportsConfirmation
            ? InviteLinkEligibilityValidationService.ValidateConfirmMembershipStatus(request.Organization!, request.ExistingOrganizationUser)
            : InviteLinkEligibilityValidationService.ValidateAcceptMembershipStatus(request.Organization!, request.ExistingOrganizationUser);
}
