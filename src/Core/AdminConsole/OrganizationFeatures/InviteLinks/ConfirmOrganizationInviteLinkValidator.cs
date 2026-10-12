using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;
using PasswordManagerValidation = Bit.Core.AdminConsole.Utilities.Validation;

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
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IPricingClient pricingClient,
    IJoinOrganizationPolicyValidationService joinOrganizationPolicyValidationService)
    : IConfirmOrganizationInviteLinkValidator
{
    public async Task<ValidationResult<ConfirmOrganizationInviteLinkValidationRequest>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
    {
        var user = request.User;
        var link = request.InviteLink;
        var existingOrganizationUser = request.ExistingOrganizationUser;

        if (link is null || !link.CodeMatches(request.Code.ToString()))
        {
            return Invalid(request, new InviteLinkNotFound());
        }

        var organization = request.Organization;
        if (organization is null or { Enabled: false })
        {
            return Invalid(request, new InviteLinkNotFound());
        }

        if (!organization.UseInviteLinks)
        {
            return Invalid(request, new ConfirmInviteLinkNotAvailable());
        }

        if (!link.SupportsConfirmation)
        {
            return Invalid(request, new ConfirmInviteLinkConfirmationNotSupported());
        }

        if (!user.EmailVerified)
        {
            return Invalid(request, new ConfirmEmailNotVerified());
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            return Invalid(request, new ConfirmEmailDomainNotAllowed(organization.DisplayName()));
        }

        var membershipStatusError = ValidateExistingMembershipStatus(existingOrganizationUser, organization.DisplayName());
        if (membershipStatusError is not null)
        {
            return Invalid(request, membershipStatusError);
        }

        var freeAdminError = await ValidateFreeOrganizationAdminLimitAsync(organization, existingOrganizationUser, user);
        if (freeAdminError is not null)
        {
            return Invalid(request, freeAdminError);
        }

        // A seat is only consumed when a brand-new membership will be created or a Staged membership is
        // promoted, since Staged rows are excluded from the occupied seat count. An existing pending
        // invitation already occupies a seat, so confirming it adds no capacity pressure.
        if (existingOrganizationUser is null or { Status: OrganizationUserStatusType.Staged })
        {
            var seatError = await ValidatePasswordManagerSeatsAsync(organization);
            if (seatError is not null)
            {
                return Invalid(request, seatError);
            }
        }

        // The user joins with their existing membership's role, or as a User when they are a brand-new member.
        var policyError = await joinOrganizationPolicyValidationService.ValidateCanJoinAsync(
            organization, user, existingOrganizationUser?.Type ?? OrganizationUserType.User);
        if (policyError is not null)
        {
            return Invalid(request, ConfirmOrganizationInviteLinkErrorMapper.ToValidationError(policyError));
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

    private async Task<Error?> ValidateFreeOrganizationAdminLimitAsync(
        Organization targetOrganization, OrganizationUser? existingOrganizationUser, User user)
    {
        if (existingOrganizationUser?.Type is OrganizationUserType.Owner
                or OrganizationUserType.Admin
                && targetOrganization.PlanType == PlanType.Free
                && await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(user.Id) > 0)
        {
            return new ConfirmOnlyOneFreeOrganizationAdminAllowed();
        }

        return null;
    }

    /// <summary>
    /// Reuses the Password Manager seat validation to confirm the organization can accommodate one more
    /// member. This covers having Password Manager seats, the plan allowing additional seats, the max
    /// additional seats, and the autoscale seat limit.
    /// </summary>
    private async Task<Error?> ValidatePasswordManagerSeatsAsync(Organization organization)
    {
        var plan = await pricingClient.GetPlan(organization.PlanType);
        var inviteOrganization = new InviteOrganization(organization, plan);
        var occupiedSeats = (await organizationRepository
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(inviteOrganization, occupiedSeats, newUsersToAdd: 1);

        return InviteUsersPasswordManagerValidator.ValidatePasswordManager(subscriptionUpdate)
            is PasswordManagerValidation.Invalid<PasswordManagerSubscriptionUpdate>
            ? new ConfirmOrganizationHasNoAvailableSeats(organization.DisplayName())
            : null;
    }
}
