using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;
using PasswordManagerValidation = Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkOrganizationCapabilityValidator"/>.
/// </summary>
public class InviteLinkOrganizationCapabilityValidator(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IPricingClient pricingClient)
    : IInviteLinkOrganizationCapabilityValidator
{
    public async Task<ValidationResult<InviteLinkOrganizationCapabilityValidationRequest>> ValidateAsync(
        InviteLinkOrganizationCapabilityValidationRequest request)
    {
        var organization = request.Organization;
        var existingOrganizationUser = request.ExistingOrganizationUser;

        // An email invite can carry an Admin/Owner role, so enforce the "one admin of a Free org" rule (a plan
        // constraint, not a policy), mirroring AcceptOrgUserCommand. A new member always joins as User.
        if (existingOrganizationUser?.Type is OrganizationUserType.Owner or OrganizationUserType.Admin
            && organization.PlanType == PlanType.Free
            && await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(request.User.Id) > 0)
        {
            return Invalid(request, new OnlyOneFreeOrganizationAdminAllowed());
        }

        // A seat is only consumed when a brand-new membership is created or a Staged membership is promoted, since
        // Staged rows are excluded from the occupied seat count. Any other existing membership already occupies one.
        if (existingOrganizationUser is null or { Status: OrganizationUserStatusType.Staged }
            && !await HasAvailablePasswordManagerSeatAsync(request))
        {
            return Invalid(request, new OrganizationHasNoAvailableSeats(organization.DisplayName()));
        }

        return Valid(request);
    }

    /// <summary>
    /// Reuses the Password Manager seat validation to confirm the organization can accommodate one more member.
    /// This covers having Password Manager seats, the plan allowing additional seats, the max additional seats,
    /// and the autoscale seat limit.
    /// </summary>
    private async Task<bool> HasAvailablePasswordManagerSeatAsync(InviteLinkOrganizationCapabilityValidationRequest request)
    {
        var organization = request.Organization;

        var occupiedSeatCount = (await organizationRepository
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;

        var plan = await pricingClient.GetPlan(organization.PlanType);

        var subscriptionUpdate = new PasswordManagerSubscriptionUpdate(
            new InviteOrganization(organization, plan), occupiedSeatCount, newUsersToAdd: 1);

        return InviteUsersPasswordManagerValidator.ValidatePasswordManager(subscriptionUpdate)
            is not PasswordManagerValidation.Invalid<PasswordManagerSubscriptionUpdate>;
    }
}
