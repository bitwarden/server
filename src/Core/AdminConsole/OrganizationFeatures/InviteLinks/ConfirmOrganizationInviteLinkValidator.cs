using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using PasswordManagerValidation = Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// Shared read-only precheck for the invite link confirmation flow. See
/// <see cref="IConfirmOrganizationInviteLinkValidator"/> for the checks performed.
/// </summary>
/// <remarks>
/// This intentionally mirrors the eligibility checks in <see cref="AcceptOrganizationInviteLinkCommand"/>
/// without duplicating its write side effects, so the confirmation endpoints can verify a user before
/// they are given the organization key. Write-time concerns (e.g. creating the default collection,
/// auto-scaling seats) are left to the consuming command.
/// </remarks>
public class ConfirmOrganizationInviteLinkValidator(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository,
    IPricingClient pricingClient,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery)
    : IConfirmOrganizationInviteLinkValidator
{
    public async Task<CommandResult<ConfirmOrganizationInviteLinkValidationResult>> ValidateAsync(
        ConfirmOrganizationInviteLinkValidationRequest request)
    {
        var user = request.User;

        var link = await organizationInviteLinkRepository.GetByCodeAsync(request.Code);
        if (link is null)
        {
            return new InviteLinkNotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(link.OrganizationId);
        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        if (!InviteLinkDomainValidator.IsEmailDomainAllowed(user.Email, link.GetAllowedDomains()))
        {
            return new EmailDomainNotAllowed();
        }

        // Provider users cannot confirm via invite links.
        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new ProviderUsersCannotAcceptInviteLink();
        }

        var existingOrganizationUser = await ResolveExistingOrganizationUserAsync(organization, user);

        var membershipStatusError = ValidateExistingMembershipStatus(existingOrganizationUser);
        if (membershipStatusError is not null)
        {
            return membershipStatusError;
        }

        // A seat is only consumed when a brand-new membership will be created. An existing pending
        // invitation already occupies a seat, so confirming it adds no capacity pressure.
        if (existingOrganizationUser is null)
        {
            var seatError = await ValidatePasswordManagerSeatsAsync(organization);
            if (seatError is not null)
            {
                return seatError;
            }
        }

        var policyError = await ValidatePoliciesAsync(organization.Id, user);
        if (policyError is not null)
        {
            return policyError;
        }

        return new ConfirmOrganizationInviteLinkValidationResult
        {
            InviteLink = link,
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
        };
    }

    /// <summary>
    /// Resolves the membership to confirm, preferring an existing user-linked membership and falling
    /// back to a pending email invitation for the same address.
    /// </summary>
    private async Task<OrganizationUser?> ResolveExistingOrganizationUserAsync(Organization organization, User user)
    {
        var userLinkedOrganizationUser = await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        if (userLinkedOrganizationUser is not null)
        {
            return userLinkedOrganizationUser;
        }

        return await organizationUserRepository.GetByOrganizationEmailAsync(organization.Id, user.Email);
    }

    private static Error? ValidateExistingMembershipStatus(OrganizationUser? existingOrganizationUser) =>
        existingOrganizationUser switch
        {
            { RevocationReason: not null } => new OrganizationAccessRevoked(),
            { Status: OrganizationUserStatusType.Confirmed } => new AlreadyOrganizationMember(),
            _ => null
        };

    /// <summary>
    /// Enforces the membership policies that gate confirmation: Single Organization and Require Two-Factor
    /// Authentication. These are read-only checks; no enrollment or revocation side effects are performed.
    /// </summary>
    private async Task<Error?> ValidatePoliciesAsync(
        Guid organizationId, User user)
    {
        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);

        var singleOrgRequirement = await policyRequirementQuery
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organizationId, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        if (!await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            var twoFactorRequirement = await policyRequirementQuery
                .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id);
            if (twoFactorRequirement.IsTwoFactorRequiredForOrganization(organizationId))
            {
                return new TwoFactorRequiredForMembership();
            }
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
            ? new OrganizationHasNoAvailableSeats()
            : null;
    }
}
