using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IGetOrganizationInviteValidator"/>.
/// </summary>
public class GetOrganizationInviteValidator : IGetOrganizationInviteValidator
{
    public Task<ValidationResult<OrganizationInviteLinkValidationRequest>> ValidateAsync(
        OrganizationInviteLinkValidationRequest request)
    {
        var eligibilityError = InviteLinkRules.ValidateEligibility(
            request.InviteLink, request.Code, request.Organization, request.User);
        if (eligibilityError is not null)
        {
            return Task.FromResult(Invalid(request, eligibilityError));
        }

        // The eligibility check guarantees both exist.
        var validLink = request.InviteLink!;
        var validOrganization = request.Organization!;

        var membershipStatusError = ValidateMembershipStatus(validLink, validOrganization, request.ExistingOrganizationUser);
        if (membershipStatusError is not null)
        {
            return Task.FromResult(Invalid(request, membershipStatusError));
        }

        return Task.FromResult(Valid(request));
    }

    /// <summary>
    /// The existing membership must allow the step the link leads to: confirming, or accepting.
    /// </summary>
    private static Error? ValidateMembershipStatus(
        OrganizationInviteLink inviteLink, Organization organization, OrganizationUser? existingOrganizationUser) =>
        inviteLink.SupportsConfirmation
            ? InviteLinkRules.ValidateConfirmMembershipStatus(organization, existingOrganizationUser)
            : InviteLinkRules.ValidateAcceptMembershipStatus(organization, existingOrganizationUser);
}
