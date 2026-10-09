using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Read-only precheck for the invite link confirmation flow. See
/// <see cref="IConfirmOrganizationInviteLinkValidator"/> for the checks performed.
/// </summary>
/// <remarks>
/// This performs the eligibility checks without any write side effects, so the confirmation endpoints
/// can verify a user before they are given the organization key. Write-time concerns (e.g. creating the
/// default collection, auto-scaling seats) are left to the consuming command.
/// </remarks>
public class ConfirmOrganizationInviteLinkValidator(
    IInviteLinkEligibilityValidator inviteLinkEligibilityValidator,
    IInviteLinkFreeOrganizationAdminValidator inviteLinkFreeOrganizationAdminValidator,
    IInviteLinkOrganizationCapabilityValidator inviteLinkOrganizationCapabilityValidator,
    IInviteLinkPolicyValidator inviteLinkPolicyValidator)
    : IConfirmOrganizationInviteLinkValidator
{
    public async Task<ValidationResult<ConfirmOrganizationInviteLinkValidationRequest>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
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
            return Invalid(request, ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(eligibilityResult.AsError));
        }

        // The eligibility check guarantees both exist.
        var validLink = request.InviteLink!;
        var validOrganization = request.Organization!;

        if (!validLink.SupportsConfirmation)
        {
            return Invalid(request, new ConfirmInviteLinkConfirmationNotSupported());
        }

        var membershipStatusError = ValidateExistingMembershipStatus(existingOrganizationUser, validOrganization.DisplayName());
        if (membershipStatusError is not null)
        {
            return Invalid(request, membershipStatusError);
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
            return Invalid(request,
                ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(freeOrganizationAdminResult.AsError));
        }

        var capabilityResult = await inviteLinkOrganizationCapabilityValidator.ValidateAsync(
            new InviteLinkOrganizationCapabilityValidationRequest
            {
                Organization = validOrganization,
                ExistingOrganizationUser = existingOrganizationUser,
            });
        if (capabilityResult.IsError)
        {
            return Invalid(request, ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(capabilityResult.AsError));
        }

        var policyResult = await inviteLinkPolicyValidator.ValidateAsync(new InviteLinkPolicyValidationRequest
        {
            Organization = validOrganization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        });
        if (policyResult.IsError)
        {
            return Invalid(request, ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(policyResult.AsError));
        }

        return Valid(request);
    }

    private static Error? ValidateExistingMembershipStatus(OrganizationUser? existingOrganizationUser, string orgName) =>
        existingOrganizationUser switch
        {
            { RevocationReason: not null } => new ConfirmOrganizationAccessRevoked(orgName),
            { Status: OrganizationUserStatusType.Confirmed } => new ConfirmAlreadyOrganizationMember(orgName),
            _ => null
        };
}
