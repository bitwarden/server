using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

/// <summary>
/// See <see cref="IInviteLinkPolicyValidator"/>.
/// </summary>
public class InviteLinkPolicyValidator(
    IPreAccessEnforcerQuery preAccessEnforcerQuery,
    IPolicyRequirementQuery policyRequirementQuery,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    IOrganizationUserRepository organizationUserRepository,
    IProviderUserRepository providerUserRepository)
    : IInviteLinkPolicyValidator
{
    public async Task<CommandResult<InviteLinkPolicyValidationResult>> ValidateAsync(
        InviteLinkPolicyValidationRequest request)
    {
        var user = request.User;
        var organizationId = request.Organization.Id;
        var proposedRole = GetProposedRole(request);

        var allOrganizationMemberships = await organizationUserRepository.GetManyByUserAsync(user.Id);
        var policyEnforcer = await preAccessEnforcerQuery.RunAsync(organizationId);

        var singleOrgError = await ValidateSingleOrganizationPolicyAsync(request, policyEnforcer, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        var twoFactorError = await ValidateTwoFactorAuthenticationPolicyAsync(user, proposedRole, policyEnforcer);
        if (twoFactorError is not null)
        {
            return twoFactorError;
        }

        var autoConfirmPolicyEnabled = policyEnforcer
            .Evaluate(PolicyType.AutomaticUserConfirmation, user.Id, proposedRole).IsEnforced;
        var autoConfirmError = await ValidateAutomaticUserConfirmationPolicyAsync(
            user, organizationId, autoConfirmPolicyEnabled, allOrganizationMemberships);
        if (autoConfirmError is not null)
        {
            return autoConfirmError;
        }

        return new InviteLinkPolicyValidationResult
        {
            AllOrganizationMemberships = allOrganizationMemberships,
            AutoConfirmPolicyEnabled = autoConfirmPolicyEnabled,
        };
    }

    /// <summary>
    /// The Single Organization policy: another organization the user belongs to may forbid joining this one, and
    /// this organization may forbid belonging to any other.
    /// </summary>
    private async Task<Error?> ValidateSingleOrganizationPolicyAsync(InviteLinkPolicyValidationRequest request,
        IPreAccessPolicyEnforcer policyEnforcer, ICollection<OrganizationUser> allOrganizationMemberships)
    {
        var user = request.User;
        var organizationId = request.Organization.Id;

        // Other organizations. This also checks this organization when the requirement framework can resolve the
        // user's membership in it; that is consistent with the check below.
        var singleOrgRequirement = await policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id);
        var singleOrgError = singleOrgRequirement.CanJoinOrganization(organizationId, allOrganizationMemberships);
        if (singleOrgError is not null)
        {
            return singleOrgError;
        }

        // This organization
        if (allOrganizationMemberships.Any(ou => ou.OrganizationId != organizationId)
            && policyEnforcer.Evaluate(PolicyType.SingleOrg, user.Id, GetProposedRole(request)).IsEnforced)
        {
            return new UserIsAMemberOfAnotherOrganization();
        }

        return null;
    }

    /// <summary>
    /// The Required Two-Factor Authentication policy of this organization.
    /// </summary>
    private async Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(
        User user, OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer)
    {
        if (policyEnforcer.Evaluate(PolicyType.TwoFactorAuthentication, user.Id, proposedRole).IsEnforced
            && !await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return new TwoFactorRequiredForMembership();
        }

        return null;
    }

    /// <summary>
    /// The Automatic User Confirmation policy: another organization the user belongs to may forbid joining this one,
    /// and when this organization enforces it, the user cannot be a provider user or belong to any other
    /// organization (no role exemptions).
    /// </summary>
    private async Task<Error?> ValidateAutomaticUserConfirmationPolicyAsync(User user, Guid organizationId,
        bool autoConfirmPolicyEnabled, ICollection<OrganizationUser> allOrganizationMemberships)
    {
        // Other organizations
        var autoConfirmRequirement = await policyRequirementQuery
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id);
        if (autoConfirmRequirement.IsEnabledForOrganizationsOtherThan(organizationId))
        {
            return new OtherOrganizationDoesNotAllowOtherMembership(user.Email);
        }

        // This organization
        if (!autoConfirmPolicyEnabled)
        {
            return null;
        }

        if ((await providerUserRepository.GetManyByUserAsync(user.Id)).Count != 0)
        {
            return new ProviderUsersCannotAcceptInviteLink();
        }

        if (allOrganizationMemberships.Any(ou => ou.OrganizationId != organizationId))
        {
            return new UserCannotBelongToAnotherOrganization(user.Email);
        }

        return null;
    }

    /// <summary>
    /// The role the user will hold in the organization: their existing membership's role, or
    /// <see cref="OrganizationUserType.User"/> for a brand-new member.
    /// </summary>
    private static OrganizationUserType GetProposedRole(InviteLinkPolicyValidationRequest request) =>
        request.ExistingOrganizationUser?.Type ?? OrganizationUserType.User;
}
