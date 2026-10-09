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
    IConfirmInviteLinkMembershipStatusValidator confirmInviteLinkMembershipStatusValidator,
    IInviteLinkFreeOrganizationAdminValidator inviteLinkFreeOrganizationAdminValidator,
    IInviteLinkOrganizationCapabilityValidator inviteLinkOrganizationCapabilityValidator,
    IInviteLinkPolicyValidator inviteLinkPolicyValidator)
    : IGetOrganizationInviteValidator
{
    public async Task<ValidationResult<OrganizationInviteLinkValidationRequest>> ValidateAsync(
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
            return Invalid(request, eligibilityResult.AsError);
        }

        // The eligibility check guarantees both exist.
        var validLink = request.InviteLink!;
        var validOrganization = request.Organization!;

        var membershipStatusResult = ValidateMembershipStatus(validLink, validOrganization, existingOrganizationUser);
        if (membershipStatusResult.IsError)
        {
            return Invalid(request, membershipStatusResult.AsError);
        }

        var freeOrganizationAdminResult = await inviteLinkFreeOrganizationAdminValidator.ValidateAsync(
            new InviteLinkFreeOrganizationAdminValidationRequest
            {
                Organization = validOrganization,
                User = user,
                ExistingOrganizationUser = existingOrganizationUser,
            });
        if (freeOrganizationAdminResult.IsError)
        {
            return Invalid(request, freeOrganizationAdminResult.AsError);
        }

        var capabilityResult = await inviteLinkOrganizationCapabilityValidator.ValidateAsync(
            new InviteLinkOrganizationCapabilityValidationRequest
            {
                Organization = validOrganization,
                ExistingOrganizationUser = existingOrganizationUser,
            });
        if (capabilityResult.IsError)
        {
            return Invalid(request, capabilityResult.AsError);
        }

        var policyResult = await inviteLinkPolicyValidator.ValidateAsync(new InviteLinkPolicyValidationRequest
        {
            Organization = validOrganization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        });
        if (policyResult.IsError)
        {
            return Invalid(request, policyResult.AsError);
        }

        return Valid(request);
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
