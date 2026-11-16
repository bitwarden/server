using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IGetOrganizationInviteValidator"/>.
/// </summary>
public class GetOrganizationInviteValidator(
    IInviteLinkValidationService inviteLinkValidationService,
    IPreAccessEnforcerQuery preAccessEnforcerQuery)
    : IGetOrganizationInviteValidator
{
    public async Task<ValidationResult<OrganizationInviteLinkValidationRequest>> ValidateAsync(
        OrganizationInviteLinkValidationRequest request)
    {
        var eligibilityError = inviteLinkValidationService.ValidateEligibility(
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

        // ValidateEligibility guarantees the organization exists.
        var organization = request.Organization!;

        var seatAvailabilityError = await inviteLinkValidationService.ValidateSeatAvailabilityAsync(
            organization,
            request.ExistingOrganizationUser);
        if (seatAvailabilityError is not null)
        {
            return Invalid(request, seatAvailabilityError);
        }

        var joinRequirementsResult = await inviteLinkValidationService.ValidateJoinRequirementsAsync(
            organization,
            request.User,
            request.ExistingOrganizationUser?.Type ?? OrganizationUserType.User,
            await preAccessEnforcerQuery.RunAsync(organization.Id));

        if (joinRequirementsResult.IsError)
        {
            return Invalid(request, ToGetInviteValidationError(joinRequirementsResult.Error!));
        }

        return Valid(request);
    }

    /// <summary>
    /// Keeps the invite link error messages for shared errors that have an invite link equivalent, matching the
    /// accept endpoint.
    /// </summary>
    // TODO: This only exists in order to satisfy fragile client-side error parsing.
    // TODO: Remove this with PM-40926 once we are returning stable error codes.
    private static Error ToGetInviteValidationError(Error error) =>
        error switch
        {
            CannotBeAdminOfMultipleFreeOrganizations => new OnlyOneFreeOrganizationAdminAllowed(),
            ProviderUsersCannotJoin => new ProviderUsersCannotAcceptInviteLink(),
            _ => error
        };

    /// <summary>
    /// The existing membership must allow the step the link leads to: confirming, or accepting.
    /// </summary>
    /// <remarks>
    /// Must run after <see cref="IInviteLinkValidationService.ValidateEligibility"/>, which guarantees the invite link and
    /// organization exist.
    /// </remarks>
    private static Error? ValidateMembershipStatus(OrganizationInviteLinkValidationRequest request) =>
        request.ExistingInviteLink!.SupportsConfirmation
            ? InviteLinkRules.ValidateConfirmMembershipStatus(request.Organization!, request.ExistingOrganizationUser)
            : InviteLinkRules.ValidateAcceptMembershipStatus(request.Organization!, request.ExistingOrganizationUser);
}
