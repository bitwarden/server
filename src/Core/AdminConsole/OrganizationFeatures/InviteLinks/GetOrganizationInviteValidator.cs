using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IGetOrganizationInviteValidator"/>.
/// </summary>
public class GetOrganizationInviteValidator(
    IInviteLinkEligibilityValidator inviteLinkEligibilityValidator,
    IAcceptInviteLinkMembershipStatusValidator acceptInviteLinkMembershipStatusValidator,
    IConfirmInviteLinkMembershipStatusValidator confirmInviteLinkMembershipStatusValidator)
    : IGetOrganizationInviteValidator
{
    public Task<ValidationResult<OrganizationInviteLinkValidationRequest>> ValidateAsync(
        OrganizationInviteLinkValidationRequest request)
    {
        var user = request.User;
        var existingOrganizationUser = request.ExistingOrganizationUser;

        var eligibilityResult = inviteLinkEligibilityValidator.Validate(new InviteLinkEligibilityValidationRequest
        {
            InviteLink = request.InviteLink,
            Code = request.Code,
            Organization = request.Organization,
            User = user,
        });
        if (eligibilityResult.IsError)
        {
            return Task.FromResult(Invalid(request, eligibilityResult.AsError));
        }

        // The eligibility check guarantees both exist.
        var validLink = request.InviteLink!;
        var validOrganization = request.Organization!;

        var membershipStatusResult = ValidateMembershipStatus(validLink, validOrganization, existingOrganizationUser);
        if (membershipStatusResult.IsError)
        {
            return Task.FromResult(Invalid(request, membershipStatusResult.AsError));
        }

        return Task.FromResult(Valid(request));
    }

    /// <summary>
    /// The existing membership must allow the step the link leads to: confirming, or accepting.
    /// </summary>
    private ValidationResult<InviteLinkMembershipStatusValidationRequest> ValidateMembershipStatus(
        OrganizationInviteLink inviteLink, Organization organization, OrganizationUser? existingOrganizationUser)
    {
        var request = new InviteLinkMembershipStatusValidationRequest
        {
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
        };

        if (inviteLink.SupportsConfirmation)
        {
            return confirmInviteLinkMembershipStatusValidator.Validate(request);
        }

        return acceptInviteLinkMembershipStatusValidator.Validate(request);
    }
}
